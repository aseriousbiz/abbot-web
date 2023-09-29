using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI_API.Chat;
using Refit;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.Entities;
using Serious.Slack;
using Serious.Slack.BlockKit;
using AIChatMessage = OpenAI_API.Chat.ChatMessage;
using ChatMessage = Serious.Abbot.Eventing.Messages.ChatMessage;

namespace Serious.Abbot.AI.Responder;

/// <summary>
/// Options to configure the <see cref="MagicResponder"/>
/// </summary>
public class MagicResponderOptions
{
    /// <summary>
    /// The maximum number of <see cref="ResponderIteration"/>s that can take place in a single <see cref="ResponderTurn"/>.
    /// Used to avoid getting lost in the Language Model.
    /// </summary>
    public int MaximumIterationsPerTurn { get; set; } = 3;
}

public class MagicResponder
{
    readonly IOptions<MagicResponderOptions> _options;
    readonly ISlackApiClient _slackApiClient;
    readonly CommandParser _commandParser;
    readonly IOpenAIClient _openAIClient;
    readonly CommandExecutor _commandExecutor;
    readonly ILogger<MagicResponder> _logger;

    public MagicResponder(
        IOptions<MagicResponderOptions> options,
        ISlackApiClient slackApiClient,
        CommandParser commandParser,
        IOpenAIClient openAIClient,
        CommandExecutor commandExecutor,
        ILogger<MagicResponder> logger)
    {
        _options = options;
        _slackApiClient = slackApiClient;
        _commandParser = commandParser;
        _openAIClient = openAIClient;
        _commandExecutor = commandExecutor;
        _logger = logger;
    }

    /// <summary>
    /// Runs a single turn of the provided <see cref="ResponderSession"/>, given the provided <see cref="Eventing.Messages.ChatMessage"/> from the user.
    /// </summary>
    /// <param name="session">The <see cref="ResponderSession"/> in which the message was received.</param>
    /// <param name="userMessage">The <see cref="Eventing.Messages.ChatMessage"/> that triggered the turn.</param>
    /// <returns>The new <see cref="ResponderTurn"/> (which will also have been added to the <see cref="ResponderSession"/>).</returns>
    public async Task<ResponderTurn> RunTurnAsync(ResponderSession session, ChatMessage userMessage)
    {
        var turnNumber = session.Turns.Count;
        using var turnScope = _logger.BeginTurnScope(session.SessionId, turnNumber);
        var turn = new ResponderTurn(userMessage);

        if (session.DebugMode)
        {
            // Dump the system prompt
            var content = new ByteArrayPart(
                Encoding.UTF8.GetBytes(session.SystemPrompt.Content),
                $"{session.SessionId}.system-prompt.txt",
                "text/plain");

            var resp = await _slackApiClient.Files.UploadFileAsync(
                session.Organization.RequireAndRevealApiToken(),
                content,
                $"{session.SessionId}.system-prompt.txt",
                "txt",
                session.Room.PlatformRoomId,
                "System Prompt for this Turn",
                session.ThreadId);

            if (!resp.Ok)
            {
                await SendMessageAsync(session, $"> :warning: Failed to post system prompt to Slack: `{resp.Error}`.");
            }

            await SendMessageAsync(session,
                $"> _Beginning Turn {turnNumber}_");
        }

        // Construct the message we'll send to the AI for the first iteration.
        var requestMessage = FormatUserMessage(session, userMessage);

        // Load any existing the chat history into a single flat list for convenience
        var languageModelHistory = session.GetLanguageModelHistory();

        // Iterate until we hit the maximum or reach the end of the turn.
        var turnComplete = false;
        EndTurnResult? turnEndResult = null;
        while (!turnComplete && turn.Iterations.Count < _options.Value.MaximumIterationsPerTurn)
        {
            var iterationNumber = turn.Iterations.Count;
            using var iterationScope = _logger.BeginIterationScope(iterationNumber);

            if (session.DebugMode)
            {
                await SendMessageAsync(session,
                    $"> _Beginning Iteration {turnNumber}.{iterationNumber}_");
            }

            try
            {
                var iteration = await RunIterationAsync(session, turn, languageModelHistory, requestMessage);
                turn.Iterations.Add(iteration);

                // Perform the result
                switch (iteration.Result)
                {
                    case EndTurnResult r:
                        turnEndResult = r;
                        break;
                    case ContinueTurnResult(var req):
                        requestMessage = new AIChatMessage(ChatMessageRole.User, FormatRequest(req));
                        break;
                }

                turnComplete = iteration.Result.EndOfTurn;
                _logger.IterationComplete(turnComplete);
                if (session.DebugMode)
                {
                    await SendMessageAsync(session,
                        $"> :checkered_flag: _Finished Iteration {turnNumber}.{iterationNumber}_");
                }
            }
            catch (Exception ex)
            {
                _logger.ExceptionDuringIteration(ex);

                if (session.DebugMode)
                {
                    await SendMessageAsync(session, $"_Exception during iteration:_\n```\n{ex}\n```");
                }

                // Let the user know.
                await SendMessageAsync(session, "_Sorry. I couldn't figure out the answer to your question._");

                // Don't retry, for now. Just return
                return turn;
            }
        }

        if (!turnComplete)
        {
            await SendMessageAsync(session, "_Sorry. I couldn't figure out the answer to your question._");

            throw new InvalidOperationException("Exceeded maximum iteration count without ending the turn.");
        }

        if (session.DebugMode)
        {
            await SendMessageAsync(session,
                $"> :checkered_flag: _Finished Turn {turnNumber}_");
        }

        if (turnEndResult is EndTurnResult({ } message, var synthesized))
        {
            var m = new MessageRequest(session.Room.PlatformRoomId, message)
            {
                ThreadTs = session.ThreadId,
                Blocks = new ILayoutBlock[]
                {
                    new Section(new MrkdwnText(message)),
                    new Context(synthesized
                        ? $":brain: This result comes from the {session.Organization.BotName} AI and may not be completely accurate."
                        : ":brain: This result comes from your organization's knowledge base.")
                },
            };

            await _slackApiClient.PostMessageWithRetryAsync(session.Organization.RequireAndRevealApiToken(), m);
        }

        turn.Complete = true;
        session.Turns.Add(turn);
        _logger.TurnComplete();
        return turn;
    }

    async Task<ResponderIteration> RunIterationAsync(ResponderSession session, ResponderTurn turn,
        IList<AIChatMessage> history, AIChatMessage nextRequest)
    {
        // Generate the next assistant message
        history.Add(nextRequest);
        if (session.DebugMode)
        {
            await SendMessageAsync(session, $"> :arrow_right: to Language Model:\n```\n{nextRequest.Content}\n```");
        }

        var response = await GetLanguageModelResponseAsync(session, history);
        history.Add(response);

        // Display the raw response in debug mode
        if (session.DebugMode)
        {
            await SendMessageAsync(session, $"> :arrow_left: from Language Model:\n```\n{response.Content}\n```");
        }

        // Parse the assistant message
        var commands = _commandParser.ParseCommands(response.Content).Require();

        // TODO: This is where we could log an audit record for the iteration.

        if (session.DebugMode)
        {
            await SendCommandListAsync(session, commands);
        }

        // For now, we only support a single command in an iteration
        if (commands.Action.Count > 1)
        {
            throw new InvalidOperationException("Cannot execute multiple commands in a single iteration.");
        }

        var command = commands.Action.Single();

        // Execute the command and generate our result.
        var result = await _commandExecutor.ExecuteCommandAsync(session, command);

        // And this iteration is done!
        return new ResponderIteration(
            nextRequest,
            response,
            commands,
            result);
    }

    async Task<AIChatMessage> GetLanguageModelResponseAsync(ResponderSession session, IList<AIChatMessage> history)
    {
        var chatResult = await _openAIClient.GetChatResultAsync(
            history,
            session.ModelSettings.Model,
            session.ModelSettings.Temperature,
            session.Initiator);

        var assistantMessage = chatResult.Choices is [{ } choice, ..]
            ? choice
            : throw new UnreachableException();

        Expect.True(assistantMessage.Message.Role.Equals(ChatMessageRole.Assistant), "Expected assistant message");
        return assistantMessage.Message;
    }

    static AIChatMessage FormatUserMessage(ResponderSession session, ChatMessage userMessage)
    {
        // We're assuming the user message is from the session Initiator.
        // In our current implementation this is safe because we only allow a single user to interact with Abbot in a session.
        // That will likely change though!
        // ChatMessage only carries IDs, not resolved entities, so we'll probably want to cache other users/etc. in the session.

        Expect.True(session.Initiator == userMessage.Event.SenderId);
        var message = $"{session.Initiator.DisplayName} (ID: {session.Initiator.ToMention()}) sent: {userMessage.Text}";
        return new(ChatMessageRole.User, FormatRequest(message));
    }

    /// <summary>
    /// Formats a request to the language model by appending the prompting suffix.
    /// </summary>
    static string FormatRequest(string message)
    {
        return
            $"""
            {message}

            Respond ONLY with the AbbotLang JSON Object that can advance this conversation.
            """;
    }

    async Task SendCommandListAsync(ResponderSession session, Reasoned<CommandList> list)
    {
        var formattedCommands = string.Join("\n", list.Action.Select(c => $"> `{c}`"));
        var m = new MessageRequest(session.Room.PlatformRoomId, list.Thought)
        {
            ThreadTs = session.ThreadId,
            Blocks = new ILayoutBlock[]
            {
                new Context(
                    $"""
                    > :brain: _{list.Thought.Replace('\n', ' ')}_
                    {formattedCommands}
                    """),
            },
        };

        await _slackApiClient.PostMessageWithRetryAsync(session.Organization.RequireAndRevealApiToken(), m);
    }

    async Task SendMessageAsync(ResponderSession session, string message)
    {
        var m = new MessageRequest(session.Room.PlatformRoomId, message)
        {
            ThreadTs = session.ThreadId,
            Blocks = new ILayoutBlock[] { new Section(new MrkdwnText(message)) },
        };

        await _slackApiClient.PostMessageWithRetryAsync(session.Organization.RequireAndRevealApiToken(), m);
    }
}

static partial class MagicResponderLoggingExtensions
{
    static readonly Func<ILogger, string, int, IDisposable?> TurnScope =
        LoggerMessage.DefineScope<string, int>(
            "Turn: {ResponderSessionId}#{ResponderTurnNumber}");

    static readonly Func<ILogger, int, IDisposable?> IterationScope =
        LoggerMessage.DefineScope<int>(
            "Iteration: {ResponderIterationNumber}");

    public static IDisposable? BeginTurnScope(
        this ILogger<MagicResponder> logger, string sessionId, int turnNumber)
        => TurnScope(logger, sessionId, turnNumber);

    public static IDisposable? BeginIterationScope(
        this ILogger<MagicResponder> logger, int iterationNumber)
        => IterationScope(logger, iterationNumber);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Exception occurred during iteration")]
    public static partial void ExceptionDuringIteration(this ILogger<MagicResponder> logger, Exception ex);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Iteration Complete (EndOfTurn: {EndOfTurn})")]
    public static partial void IterationComplete(this ILogger<MagicResponder> logger, bool endOfTurn);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Turn Complete")]
    public static partial void TurnComplete(this ILogger<MagicResponder> logger);
}

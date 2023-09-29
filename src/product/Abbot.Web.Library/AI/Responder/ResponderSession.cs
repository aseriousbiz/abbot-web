using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using AIChatMessage = OpenAI_API.Chat.ChatMessage;

namespace Serious.Abbot.AI.Responder;

/// <summary>
/// Represents an active session with the Magic Responder.
/// </summary>
/// <remarks>
/// <para>
/// This is designed to hold all the state related to a Magic Responder "Session".
/// A Session is a potentially-long-running conversation involving several users and Abbot.
/// A Session consists of several Turns, each of which is a single interaction between a user and Abbot.
/// A Turn may require several Iterations, each of which is a single interaction between Abbot and the AI system (Language Model, Commands, etc.).
/// </para>
/// <para>
/// Today, Sessions are ephemeral and are not persisted.
/// But eventually, the intent is for the <see cref="ResponderSession"/> to encapsulate all the state
/// that needs to be persisted in order to "suspend" and "resume" a session with any interactive responder.
/// </para>
/// <para>
/// NOTE: That does NOT mean the session is serializable, but it represents the state that should be serialized
/// and is intended to be able to be restored from some kind of serializable state in the future.
/// </para>
/// </remarks>
public class ResponderSession
{
    /// <summary>
    /// Identifies if the session is in debug mode.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// A unique ID for this session.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// The <see cref="Organization"/> in which this session is occurring.
    /// </summary>
    public Organization Organization { get; }

    /// <summary>
    /// The <see cref="Room"/> in which this session is occurring.
    /// </summary>
    public Room Room { get; }

    /// <summary>
    /// The ID of the thread in which this session is occurring, if any.
    /// </summary>
    public string? ThreadId { get; }

    /// <summary>
    /// The <see cref="Member"/> that initiated this session.
    /// </summary>
    public Member Initiator { get; }

    /// <summary>
    /// The <see cref="ModelSettings"/> that are being used for this session.
    /// This is captured at the start of the session and retained even when the underlying settings change.
    /// </summary>
    public ModelSettings ModelSettings { get; }

    /// <summary>
    /// The System Prompt used in this session.
    /// </summary>
    /// <remarks>
    /// The System Prompt is not expected to change during the session,
    /// but it could be allowed to change in the future (e.g. if new features are added, or if context from the conversation requires it).
    /// </remarks>
    public AIChatMessage SystemPrompt { get; }

    /// <summary>
    /// The <see cref="ResponderTurn"/>s that are part of this session.
    /// </summary>
    public IList<ResponderTurn> Turns { get; } = new List<ResponderTurn>();

    public ResponderSession(
        string sessionId,
        Organization organization,
        Room room,
        string? threadId,
        Member initiator,
        ModelSettings modelSettings,
        AIChatMessage systemPrompt)
    {
        SessionId = sessionId;
        Organization = organization;
        Room = room;
        ThreadId = threadId;
        Initiator = initiator;
        ModelSettings = modelSettings;
        SystemPrompt = systemPrompt;
    }

    /// <summary>
    /// Gets a flat list of OpenAI <see cref="AIChatMessage"/> objects that represent the entire conversation with the Language Model so far.
    /// </summary>
    /// <returns></returns>
    public IList<AIChatMessage> GetLanguageModelHistory()
    {
        var messages = new List<AIChatMessage>();
        messages.Add(SystemPrompt);
        foreach (var turn in Turns)
        {
            foreach (var iteration in turn.Iterations)
            {
                messages.Add(iteration.Request);
                messages.Add(iteration.LanguageModelResponse);
            }
        }

        return messages;
    }
}

/// <summary>
/// Represents a single Turn in a <see cref="ResponderSession"/>.
/// A Turn is a single interaction between a user and Abbot.
/// It begins with the user's message to Abbot and ends with Abbot's response.
/// </summary>
public class ResponderTurn
{
    /// <summary>
    /// The <see cref="ChatMessage"/> from the user that started this turn.
    /// </summary>
    public ChatMessage UserMessage { get; }

    /// <summary>
    /// The <see cref="ResponderIteration"/>s that are part of this turn.
    /// </summary>
    public IList<ResponderIteration> Iterations { get; } = new List<ResponderIteration>();

    /// <summary>
    /// Gets a boolean indicating if this turn is complete.
    /// </summary>
    public bool Complete { get; set; }

    public ResponderTurn(ChatMessage userMessage)
    {
        UserMessage = userMessage;
    }
}

/// <summary>
/// Represents a single Iteration in a <see cref="ResponderTurn"/>.
/// An Iteration is a single interaction between Abbot and the AI system (Language Model, Commands, etc.).
/// </summary>
public class ResponderIteration
{
    /// <summary>
    /// A <see cref="OpenAI_API.Chat.ChatMessage"/> that represents the request to the AI system.
    /// </summary>
    public AIChatMessage Request { get; }

    /// <summary>
    /// A <see cref="OpenAI_API.Chat.ChatMessage"/> that represents the response from the language model.
    /// </summary>
    public AIChatMessage LanguageModelResponse { get; }

    /// <summary>
    /// A list of <see cref="Command"/>s that were identified in the <see cref="LanguageModelResponse"/>.
    /// </summary>
    public Reasoned<CommandList> Commands { get; set; }

    /// <summary>
    /// Represents the result of this iteration.
    /// </summary>
    public IterationResult Result { get; set; }

    public ResponderIteration(AIChatMessage request, AIChatMessage languageModelResponse, Reasoned<CommandList> commands, IterationResult result)
    {
        Request = request;
        LanguageModelResponse = languageModelResponse;
        Commands = commands;
        Result = result;
    }
}

/// <summary>
/// Represents the result of an iteration.
/// </summary>
/// <param name="IsTerminal">Indicates whether this iteration represents the end of the turn.</param>
public abstract record IterationResult(bool EndOfTurn);

/// <summary>
/// Indicates that the iteration concludes the turn and should result in a response to the user.
/// </summary>
/// <param name="ResponseMessage">The message to send as a response to the user, if any.</param>
/// <param name="Synthesized">Indicates whether the response message is synthesized by the LLM or comes from data retrieved by Abbot.</param>
public record EndTurnResult(string? ResponseMessage, bool Synthesized) : IterationResult(true);

/// <summary>
/// Indicates that the iteration does not conclude the turn and should result in another iteration.
/// </summary>
/// <param name="NextRequest">The message to send as the next request to the AI system.</param>
public record ContinueTurnResult(string NextRequest) : IterationResult(false);

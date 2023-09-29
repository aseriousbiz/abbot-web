using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Logging;
using OpenAI_API.Chat;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.AI.Responder;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using AIChatMessage = OpenAI_API.Chat.ChatMessage;

namespace Serious.Abbot.Eventing;

public class MagicResponderConsumer : IConsumer<ReceivedChatMessage>
{
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly MagicResponder _magicResponder;
    readonly AISettingsRegistry _aiSettingsRegistry;
    readonly PromptCompiler _promptCompiler;
    readonly Reactor _reactor;
    readonly IClock _clock;
    readonly ILogger<MagicResponderConsumer> _logger;

    static readonly string[] FeatureList =
    {
        // Describe REM.
        // I found that the Language Model wants to ask users to help it store information there unless you tell it not to.
        "REM is a simple key-value database that can be used to retrieve values stored by users. YOU CANNOT save values to REM. DO NOT solicit information to store there.",
    };

    static readonly ISet<Type> AllowedCommands = new HashSet<Type>
    {
        typeof(NoopCommand),
        typeof(ChatPostCommand),
        typeof(RemSearchCommand),
    };
    readonly List<CommandDescriptor> _allowedCommands;

    public MagicResponderConsumer(
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        MagicResponder magicResponder,
        AISettingsRegistry aiSettingsRegistry,
        PromptCompiler promptCompiler,
        CommandRegistry registry,
        Reactor reactor,
        IClock clock,
        ILogger<MagicResponderConsumer> logger)
    {
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _magicResponder = magicResponder;
        _aiSettingsRegistry = aiSettingsRegistry;
        _promptCompiler = promptCompiler;
        _reactor = reactor;
        _clock = clock;
        _logger = logger;

        // Get allowed commands
        _allowedCommands = registry.GetAllCommands().Where(d => AllowedCommands.Contains(d.Type))
            .ToList();
    }

    public async Task Consume(ConsumeContext<ReceivedChatMessage> context)
    {
        // Fetch pre-requisite data
        var organization = context.GetPayload<Organization>();
        if (organization.ApiToken is null) // OrganizationFilter handles ignoring disabled orgs.
        {
            _logger.OrganizationHasNoSlackApiToken();
            return;
        }

        // Message events will always have a room Id.
        var roomId = context.Message.ChatMessage.Event.RoomId.Require();
        var room = await _roomRepository.GetRoomAsync(roomId);
        if (room is null)
        {
            _logger.EntityNotFound(roomId);
            return;
        }

        var sender = await _userRepository.GetMemberByIdAsync(context.Message.ChatMessage.Event.SenderId);
        if (sender is null)
        {
            _logger.EntityNotFound(context.Message.ChatMessage.Event.SenderId);
            return;
        }

        // Check if we should be in debug mode
        var debugMode = false;
        var chatMessage = context.Message.ChatMessage;
        if (sender.IsStaff() && organization.IsSerious() && chatMessage.Text.EndsWith(" [DEBUG]", StringComparison.OrdinalIgnoreCase))
        {
            // Remove the "[DEBUG]" token
            chatMessage = chatMessage with
            {
                Text = chatMessage.Text.LeftBefore(" [DEBUG]", StringComparison.OrdinalIgnoreCase),
            };

            debugMode = true;
        }

        await using var _ = await _reactor.ReactDuringAsync("sparkles",
            organization,
            room.PlatformRoomId,
            context.Message.ChatMessage.MessageId);

        // Get our model settings
        var modelSettings = await _aiSettingsRegistry.GetModelSettingsAsync(AIFeature.MagicResponder);
        var systemPrompt = await GenerateSystemPromptAsync(modelSettings, organization);

        // Set up the session
        var sessionId = Guid.NewGuid().ToString("N"); // Later, we'll use this as a key to persist/retrieve the session.
        var session = new ResponderSession(
            sessionId,
            organization,
            room,
            chatMessage.ThreadId,
            sender,
            modelSettings,
            systemPrompt)
        {
            DebugMode = debugMode,
        };

        // Run the responder
        await _magicResponder.RunTurnAsync(session, chatMessage);
    }

    async Task<AIChatMessage> GenerateSystemPromptAsync(ModelSettings settings, Organization organization)
    {
        // Compile the prompt (TODO: Cache this)
        var compiledPrompt = _promptCompiler.Compile(settings.Prompt.Text);

        // Create the template context
        // TODO: Make this a real type
        var context = new {
            PromptConstants = new PromptConstants(),
            Organization = organization,
            Features = FeatureList,
            AllowedCommands = _allowedCommands,
            CurrentTime = _clock.UtcNow.ToString("f", CultureInfo.InvariantCulture),
            ExampleResponses = new Reasoned<CommandList>[]
            {
                new(
                    """
                    Jane is looking for the LA office address.
                    Where could I find this?
                    I could find it in Rem.
                    Do I have any commands that can access rem?
                    I have the `rem.search` command.
                    I'll execute that.
                    """,
                    new CommandList(new List<Command>
                    {
                        new RemSearchCommand
                        {
                            Terms = new[] { "la", "lax", "address", "los", "angeles", "office" }
                        },
                    })),
                new(
                    """
                    Sam is looking for this month's revenue report.
                    Can I find that in REM?
                    Possibly.
                    A "month" is a unit of time, do I know today's date?
                    Yes, it's June 2023.
                    I should check for keys in REM that could have this information, using the date as a term.
                    """,
                    new CommandList(new List<Command>
                    {
                        new RemSearchCommand
                        {
                            Terms = new[] { "revenue", "report", "june", "2023" }
                        },
                    })),
                new(
                    """
                    Now I've got the 'azure portal url' value from REM.
                    Is this a sufficient answer to Steve's question?
                    Yes, it is.
                    Do I have a command I can use to respond to Steve?
                    Yes, I do, the `chat.post` command.
                    I will use `chat.post` to respond to Steve.
                    """,
                    new CommandList(new List<Command>
                    {
                        new ChatPostCommand
                        {
                            Body = "The Azure Portal URL is https://portal.azure.com"
                        },
                    })),
                new(
                    """
                    I used `rem.search` to search for 'customer call date' in REM, and found a result.
                    Can I send this to the user?
                    Yes, using `chat.post`.
                    Since this answer came directly from REM, I'll mark it as NOT synthesized.
                    """,
                    new CommandList(new List<Command>
                    {
                        new ChatPostCommand
                        {
                            Body = "<@U123> the next customer call is on April 25th, 2023.",
                            Synthesized = false,
                        },
                    })),
                new(
                    """
                    I used `rem.search` to search for matching data in REM, but found nothing.
                    Can I do anything else to find the answer?
                    No. I don't have any more commands available.
                    I'll respond to the user, but I'll mark this as synthesized since it didn't come directly from REM.
                    """,
                    new CommandList(new List<Command>
                    {
                        new ChatPostCommand
                        {
                            Body = "<@U123> the answer to life, the universe, and everything is 42, obviously.",
                            Synthesized = true,
                        },
                    })),
            },
        };

        // Render the prompt
        var prompt = compiledPrompt(context);
        return new(ChatMessageRole.System, prompt);
    }
}

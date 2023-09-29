using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Messaging.Slack;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Signals;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.PayloadHandlers;

public class ReactionHandler : IPayloadHandler<ReactionAddedEvent>, IHandler
{
    static readonly ILogger<ReactionHandler> Log = ApplicationLoggerFactory.CreateLogger<ReactionHandler>();

    static class Reactions
    {
        public const string Eyes = "eyes";
        public const string Ticket = "ticket";
        public const string WhiteCheckMark = "white_check_mark";

        public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;
    }

    static readonly HashSet<string> ReactionsToHandle = new(Reactions.Comparer)
    {
        Reactions.Eyes,
        Reactions.Ticket,
        Reactions.WhiteCheckMark,
    };

    public static readonly string AllowTicketReactionSettingName = "AllowTicketReaction";
    readonly OpenTicketMessageBuilder _openTicketMessageBuilder;
    readonly IConversationRepository _conversationRepository;
    readonly ISettingsManager _settingsManager;
    readonly ISlackApiClient _slackApiClient;
    readonly DismissHandler _dismissHandler;
    readonly ISystemSignaler _systemSignaler;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly ISlackResolver _slackResolver;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly IClock _clock;
    readonly IAnalyticsClient _analyticsClient;

    public ReactionHandler(
        OpenTicketMessageBuilder openTicketMessageBuilder,
        IConversationRepository conversationRepository,
        ISettingsManager settingsManager,
        ISlackApiClient slackApiClient,
        DismissHandler dismissHandler,
        ISystemSignaler systemSignaler,
        PlaybookDispatcher playbookDispatcher,
        ISlackResolver slackResolver,
        IBackgroundJobClient backgroundJobClient,
        IClock clock,
        IAnalyticsClient analyticsClient)
    {
        _openTicketMessageBuilder = openTicketMessageBuilder;
        _conversationRepository = conversationRepository;
        _settingsManager = settingsManager;
        _slackApiClient = slackApiClient;
        _dismissHandler = dismissHandler;
        _systemSignaler = systemSignaler;
        _playbookDispatcher = playbookDispatcher;
        _slackResolver = slackResolver;
        _backgroundJobClient = backgroundJobClient;
        _clock = clock;
        _analyticsClient = analyticsClient;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<ReactionAddedEvent> platformEvent)
    {
        if (platformEvent.Payload is not
            { Item.Channel: { } channel, Item.Timestamp: { } messageId, Reaction: { } reaction })
        {
            Log.InvalidPayload();
            return;
        }

        if (platformEvent.Organization.Enabled is false)
        {
            Log.OrganizationDisabled();
            return;
        }

        if (platformEvent.From.IsAbbot())
        {
            return;
        }

        using var _ = Log.HandleReaction(reaction, messageId, channel);

        var actor = platformEvent.From;

        if (platformEvent.Room is not { } room)
        {
            Log.IgnoreWhenRoomNull(); // This should never happen.
            return;
        }

        if (!platformEvent.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return;
        }

        // We need to get the thread Id for the message.
        var response = await _slackApiClient.Conversations.GetConversationAsync(apiToken, channel, messageId);
        if (response is null)
        {
            Log.MessageNotFound();
            // Couldn't find the message for some reason.
            return;
        }

        var firstMessageId = response.ThreadTimestamp ?? messageId;
        var conversation = await _conversationRepository.GetConversationByThreadIdAsync(
            firstMessageId,
            room,
            followHubThread: true);

        if (response.User is not null
            && await _slackResolver.ResolveMemberAsync(response.User, platformEvent.Organization) is { } author)
        {
            var messageUrl = SlackFormatter.MessageUrl(
                platformEvent.Organization.Domain,
                room.PlatformRoomId,
                messageId,
                response.ThreadTimestamp);
            _systemSignaler.EnqueueSystemSignal(
                SystemSignal.ReactionAddedSignal,
                arguments: reaction,
                platformEvent.Organization,
                room.ToPlatformRoom(),
                platformEvent.From,
                triggeringMessage: new MessageInfo(
                    messageId,
                    response.Text,
                    messageUrl,
                    response.ThreadTimestamp,
                    conversation,
                    author));

            var outputs = new OutputsBuilder()
                .SetMessage(room, response, messageUrl)
                .SetConversation(conversation)
                .SetRoom(room)
                .Outputs;
            outputs["reaction"] = reaction;
            await _playbookDispatcher.DispatchAsync(
                ReactionAddedTrigger.Id,
                outputs,
                platformEvent.Organization,
                conversation is not null
                    ? PlaybookRunRelatedEntities.From(conversation)
                    : PlaybookRunRelatedEntities.From(room));
        }

        // Eventually these may be customizable, but for now we can ignore all but known reactions
        if (!ReactionsToHandle.Contains(reaction))
        {
            Log.IgnoreUnhandled();
            return;
        }

        // Attaching to a hub overrides all other actions
        switch (await GetAllowedAction(room, reaction))
        {
            case EmojiReactionAction.None:
                Log.NoActionEnabledForOrganization();
                return;
            case EmojiReactionAction.Close when conversation is null:
            case EmojiReactionAction.Snooze when conversation is null:
                Log.IgnoreUntrackedConversation();
                return;

            case EmojiReactionAction.Close:
                if (actor.OrganizationId != platformEvent.Organization.Id)
                {
                    Log.IgnoreExternalActor();
                    return;
                }

                if (!actor.IsAgent())
                {
                    Log.IgnoreNonAgent();
                    return;
                }
                await HandleConversationCloseReactionAsync(
                    conversation,
                    platformEvent,
                    channel,
                    response);
                break;

            case EmojiReactionAction.Snooze:
                if (actor.OrganizationId != platformEvent.Organization.Id)
                {
                    Log.IgnoreExternalActor();
                    return;
                }

                if (!actor.IsAgent())
                {
                    Log.IgnoreNonAgent();
                    return;
                }
                await HandleConversationSnoozeReactionAsync(
                    conversation,
                    platformEvent,
                    channel,
                    response);
                break;

            case EmojiReactionAction.Ticket:
                await HandleTicketReactionAsync(
                    firstMessageId,
                    conversation,
                    platformEvent,
                    channel,
                    response);
                break;
        }

        _analyticsClient.Track(
            "Conversation Reaction",
            AnalyticsFeature.Reactions,
            platformEvent.From,
            platformEvent.Organization,
            new {
                reaction
            });
    }

    async Task<EmojiReactionAction> GetAllowedAction(Room room, string reaction)
    {
        return reaction switch
        {
            Reactions.Eyes or Reactions.WhiteCheckMark =>
                room.ManagedConversationsEnabled && await GetAllowReactionResponsesSetting(_settingsManager, room.Organization),
            Reactions.Ticket =>
                await GetAllowTicketReactionSetting(room),
            _ => false,
        } switch
        {
            true => GetEmojiReactionActionFromEmojiName(reaction),
            _ => EmojiReactionAction.None,
        };
    }

    static MessageTarget GetEphemeralMessageTarget(SlackMessage triggerMessage, string channel)
    {
        if (triggerMessage.ThreadTimestamp is null || triggerMessage.ThreadTimestamp == triggerMessage.Timestamp)
        {
            // This is a top-level message, so we'll send the ephemeral message to the channel.
            return new MessageTarget(new ChatAddress(ChatAddressType.Room, channel));
        }
        else
        {
            // This is in a thread, so we'll send the ephemeral message to the thread.
            return new MessageTarget(new ChatAddress(ChatAddressType.Room, channel, triggerMessage.ThreadTimestamp));
        }
    }

    async Task HandleTicketReactionAsync(
        string messageId,
        Conversation? conversation,
        IPlatformEvent platformEvent,
        string channel,
        SlackMessage triggerMessage)
    {
        Log.HandlingAction(EmojiReactionAction.Ticket);

        var openTicketMessageBlocks = (await _openTicketMessageBuilder.BuildOpenTicketMessageBlocksAsync(
                conversation,
                channel,
                messageId,
                platformEvent.Organization))
            .ToArray();

        // No active integrations? Let's not post anything.
        if (!openTicketMessageBlocks.Any())
        {
            Log.HandledActionNoop(EmojiReactionAction.Ticket);
            return;
        }

        var messageTarget = GetEphemeralMessageTarget(triggerMessage, channel);

        await platformEvent.Responder.SendEphemeralActivityAsync(
            platformEvent.From,
            fallbackText: "Please select an action.",
            messageTarget,
            openTicketMessageBlocks);

        Log.HandledAction(EmojiReactionAction.Ticket);
    }

    async Task HandleConversationCloseReactionAsync(
        Conversation conversation,
        IPlatformEvent<ReactionAddedEvent> platformEvent,
        string channel,
        SlackMessage triggerMessage)
    {
        Log.HandlingAction(EmojiReactionAction.Close);
        await _conversationRepository.CloseAsync(conversation, platformEvent.From, _clock.UtcNow, "reaction");

        await ShowEmojiResponseMessageAsync(
            platformEvent,
            "You’ve closed this conversation by replying with ✅.",
            new MrkdwnText($"You’ve closed <{conversation.GetFirstMessageUrl()}|this conversation> by replying with ✅."),
            EmojiReactionAction.Close,
            platformEvent.From,
            channel,
            triggerMessage,
            conversation);
        Log.HandledAction(EmojiReactionAction.Close);
    }

    async Task HandleConversationSnoozeReactionAsync(
        Conversation conversation,
        IPlatformEvent<ReactionAddedEvent> platformEvent,
        string channel,
        SlackMessage triggerMessage)
    {
        Log.HandlingAction(EmojiReactionAction.Snooze);
        // Move to Waiting state.
        await _conversationRepository.SnoozeConversationAsync(conversation, platformEvent.From, _clock.UtcNow);
        // Schedule the wake up.
        _backgroundJobClient.Schedule<ConversationStateChangeJob>(
            s => s.WakeAsync(conversation, platformEvent.From),
            TimeSpan.FromHours(1));

        await ShowEmojiResponseMessageAsync(
            platformEvent,
            "👀 Looks like you’re looking into this conversation. I’ll remind you of it in an hour.",
            new MrkdwnText($"👀 Looks like you’re looking into <{conversation.GetFirstMessageUrl()}|this conversation>. I’ll remind you of it in an hour."),
            EmojiReactionAction.Snooze,
            platformEvent.From,
            channel,
            triggerMessage,
            conversation);
        Log.HandledAction(EmojiReactionAction.Snooze);
    }

    async Task<bool> GetAllowTicketReactionSetting(Room room)
    {
        return await _settingsManager.GetCascadingAsync(AllowTicketReactionSettingName,
            SettingsScope.Room(room),
            SettingsScope.Organization(room.Organization)
        ) is { Value.Length: > 0 } setting && bool.Parse(setting.Value); // Disabled by default for organizations.
    }

    /// <summary>
    /// Returns the organization setting that enables/disables the :ticket: emoji reaction on a message to open a ticket.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="organization">The relevant organization.</param>
    /// <returns><c>true</c> if the reaction response are enabled, otherwise <c>false</c>.</returns>
    public static async Task<bool> GetAllowTicketReactionSetting(
        ISettingsManager settingsManager,
        Organization organization)
    {
        return await settingsManager.GetBooleanValueAsync(
            SettingsScope.Organization(organization),
            AllowTicketReactionSettingName,
            defaultIfNull: false); // Disabled by default for organizations.
    }

    /// <summary>
    /// Returns the room setting that enables/disables the :ticket: emoji reaction on a message to open a ticket.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="room">The room</param>
    /// <returns>
    /// <c>true</c> if the reaction response are enabled, <c>false</c> if the reaction response is disabled,
    /// and <c>null</c> if there is no room setting and the organization setting should be used.
    /// </returns>
    public static async Task<bool?> GetAllowTicketReactionSetting(
        ISettingsManager settingsManager,
        Room room)
    {
        var setting = await settingsManager.GetAsync(SettingsScope.Room(room), AllowTicketReactionSettingName);
        return setting is null
            ? null
            : bool.Parse(setting.Value);
    }

    /// <summary>
    /// Sets the organization setting that enables/disables the :ticket: emoji reaction on a message to open a ticket.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The user setting the setting.</param>
    /// <param name="organization">The relevant organization.</param>
    public static async Task SetAllowTicketReactionSetting(
        ISettingsManager settingsManager,
        bool value,
        User actor,
        Organization organization)
    {
        await settingsManager.SetBooleanValueAsync(
            SettingsScope.Organization(organization),
            AllowTicketReactionSettingName,
            value,
            actor);
    }

    /// <summary>
    /// Sets the room setting that enables/disables the :ticket: emoji reaction on a message to open a ticket.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The user setting the setting.</param>
    /// <param name="room">The relevant room.</param>
    public static async Task SetAllowTicketReactionSetting(
        ISettingsManager settingsManager,
        bool value,
        User actor,
        Room room)
    {
        await settingsManager.SetBooleanValueAsync(
            SettingsScope.Room(room),
            AllowTicketReactionSettingName,
            value,
            actor);
    }

    /// <summary>
    /// Returns the organization setting that enables/disables the :+1: emoji reaction on a message to close a message.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="organization">The user organization. We need this to be able to audit log these actions.</param>
    /// <returns><c>true</c> if the reaction response are enabled, otherwise <c>false</c>.</returns>
    public static async Task<bool> GetAllowReactionResponsesSetting(
        ISettingsManager settingsManager,
        Organization organization)
    {
        return await settingsManager.GetBooleanValueAsync(
            SettingsScope.Organization(organization),
            "AllowReactionResponses",
            defaultIfNull: true); // Enabled by default for organizations.
    }

    /// <summary>
    /// Sets the organization setting that enables/disables the :+1: emoji reaction on a message to close a message.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The user setting the setting.</param>
    /// <param name="organization">The user organization. We need this to be able to audit log these actions.</param>
    public static async Task SetAllowReactionResponsesSetting(
        ISettingsManager settingsManager,
        bool value,
        User actor,
        Organization organization)
    {
        await settingsManager.SetBooleanValueWithAuditing(
            SettingsScope.Organization(organization),
            "AllowReactionResponses",
            value,
            actor,
            organization);
    }

    async Task ShowEmojiResponseMessageAsync(
        IPlatformEvent<ReactionAddedEvent> platformEvent,
        string fallbackText,
        MrkdwnText text,
        EmojiReactionAction reactionAction,
        Member actor,
        string channel,
        SlackMessage triggerMessage,
        Conversation conversation)
    {
        if (!await IsEmojiResponseMessageSuppressedAsync(_settingsManager, reactionAction, actor))
        {
            var actionButtons = new List<IActionElement>
            {
                new ButtonElement("Don’t show this again",
                    new NextAction(ResponseMessageNextAction.Suppress, conversation.Id, reactionAction.ToString())),
                CommonBlockKitElements.DismissButton(),
            };

            if (reactionAction is EmojiReactionAction.Close)
            {
                actionButtons.Insert(0,
                    new ButtonElement(
                        "Reopen",
                        new NextAction(ResponseMessageNextAction.Reopen, conversation.Id, string.Empty)));
            }

            var messageTarget = GetEphemeralMessageTarget(triggerMessage, channel);

            await platformEvent.Responder.SendEphemeralActivityAsync(
                actor,
                fallbackText,
                messageTarget,
                new Section(text),
                new Actions(
                    InteractionCallbackInfo.For<ReactionHandler>(),
                    actionButtons.ToArray()));
        }
    }

    /// <summary>
    /// Handles interactions with the ephemeral message buttons.
    /// </summary>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        if (platformMessage.Payload.InteractionInfo is not { } interactionInfo)
        {
            return;
        }

        var (nextAction, conversationId, extraValue) = NextAction.Parse(interactionInfo.Arguments).Require();

        if (nextAction is ResponseMessageNextAction.Suppress)
        {
            var emojiReactionAction = Enum.Parse<EmojiReactionAction>(extraValue);
            await SuppressEmojiResponseMessageAsync(_settingsManager, emojiReactionAction, platformMessage.From);
        }
        else if (nextAction is ResponseMessageNextAction.Reopen)
        {
            if (await _conversationRepository.GetConversationAsync(conversationId) is { } conversation)
            {
                await _conversationRepository.ReopenAsync(
                    conversation,
                    platformMessage.From,
                    _clock.UtcNow,
                    ConversationState.NeedsResponse);
            }
        }

        // Any interaction will dismiss the message.
        await _dismissHandler.OnMessageInteractionAsync(platformMessage);
    }

    record NextAction(ResponseMessageNextAction Action, int ConversationId, string ExtraValue) : PrivateMetadataBase
    {
        public static NextAction? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 3, out var parts)
                ? new NextAction(
                    Enum.Parse<ResponseMessageNextAction>(parts[0]),
                    parts[1] is { Length: > 0 }
                        ? int.Parse(parts[1], CultureInfo.InvariantCulture)
                        : 0,
                    parts[2])
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return Action.ToString();
            yield return ConversationId.ToStringInvariant();
            yield return ExtraValue;
        }

        public override string ToString() => base.ToString();
    }

    // The set of actions to take in response to the message shown after the Emoji Action occurs.
    enum ResponseMessageNextAction
    {
        Suppress,
        Reopen,
    }

    /// <summary>
    /// Gets a Member setting to suppress the message in response to closing a conversation with a reaction.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="emojiReactionAction">The emoji reaction action controlled by this setting.</param>
    /// <param name="member">The member for whom the setting applies.</param>
    /// <returns><c>true</c> if the ephemeral message in response to closing a conversation with a reaction should be suppressed. Otherwise <c>false</c>.</returns>
    public static async Task<bool> IsEmojiResponseMessageSuppressedAsync(
        ISettingsManager settingsManager,
        EmojiReactionAction emojiReactionAction,
        Member member)
    {
        return await settingsManager.GetBooleanValueAsync(
            SettingsScope.Member(member),
            $"SuppressEmojiResponseMessage:{emojiReactionAction}");
    }

    /// <summary>
    /// Sets a Member setting to suppress the message in response to closing a conversation with a reaction.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="emojiReactionAction">The emoji reaction action controlled by this setting.</param>
    /// <param name="member">The member setting the setting.</param>
    public async Task SuppressEmojiResponseMessageAsync(
        ISettingsManager settingsManager,
        EmojiReactionAction emojiReactionAction,
        Member member)
    {
        await settingsManager.SetAsync(
            SettingsScope.Member(member),
            $"SuppressEmojiResponseMessage:{emojiReactionAction}",
            $"{true}",
            member.User);

        _analyticsClient.Track(
            "Conversation Reaction",
            AnalyticsFeature.Reactions,
            member,
            member.Organization,
            new {
                action = "Suppress"
            });
    }

    // Given an emoji name, this will return the action we want to take for a reaction with that emoji.
    // For now this is hard-coded, but we may make this configurable later.
    public static EmojiReactionAction GetEmojiReactionActionFromEmojiName(string emojiName)
    {
        return emojiName switch
        {
            Reactions.WhiteCheckMark => EmojiReactionAction.Close,
            Reactions.Eyes => EmojiReactionAction.Snooze,
            Reactions.Ticket => EmojiReactionAction.Ticket,
            _ => EmojiReactionAction.None
        };
    }
}

// The set of actions to take in response to an emoji reaction.
public enum EmojiReactionAction
{
    None,
    Close,
    Snooze,
    Ticket,
}

public static partial class ReactionHandlerLoggingExtensions
{
    static readonly Func<ILogger, string, string, string, IDisposable?> HandleReactionScope = LoggerMessage.DefineScope<string, string, string>(
        "Processing Reaction {Reaction} on Message {MessageId} in {PlatformRoomId}");

    public static IDisposable? HandleReaction(this ILogger<ReactionHandler> logger, string reaction, string messageId, string channel) =>
        HandleReactionScope(logger, reaction, messageId, channel);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Invalid reaction payload!")]
    public static partial void InvalidPayload(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Ignored unhandled reaction.")]
    public static partial void IgnoreUnhandled(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Ignored reaction in a null room.")]
    public static partial void IgnoreWhenRoomNull(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Ignored reaction for organization with no action enabled.")]
    public static partial void NoActionEnabledForOrganization(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Ignored reaction from external actor.")]
    public static partial void IgnoreExternalActor(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Ignored for non-Agent.")]
    public static partial void IgnoreNonAgent(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "Reaction message not found!")]
    public static partial void MessageNotFound(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Ignoring reaction for message without conversation.")]
    public static partial void IgnoreUntrackedConversation(this ILogger<ReactionHandler> logger);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Information,
        Message = "Handling reaction with action {ReactionAction}.")]
    public static partial void HandlingAction(this ILogger<ReactionHandler> logger, EmojiReactionAction reactionAction);

    [LoggerMessage(
        EventId = 18,
        Level = LogLevel.Information,
        Message = "Handled reaction with action {ReactionAction} by doing nothing.")]
    public static partial void HandledActionNoop(this ILogger<ReactionHandler> logger, EmojiReactionAction reactionAction);

    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Information,
        Message = "Handled reaction with action {ReactionAction}.")]
    public static partial void HandledAction(this ILogger<ReactionHandler> logger, EmojiReactionAction reactionAction);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Conversation {ConversationId} is already linked to Hub {HubId}")]
    public static partial void ConversationAlreadyLinkedToHub(this ILogger<ReactionHandler> logger, int conversationId, int hubId);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Information,
        Message = "Ignoring Hub Routing reaction on conversation that is already attached to a Hub")]
    public static partial void IgnoreAlreadyAttachedConversation(this ILogger<ReactionHandler> logger);
}

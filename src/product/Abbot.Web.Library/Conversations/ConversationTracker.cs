using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Conversations;

public class ConversationTracker : IConversationTracker
{
    public static readonly string DebugModeSettingName = $"{nameof(ConversationTracker)}.DebugMode";

    static readonly ILogger<ConversationTracker> Log = ApplicationLoggerFactory.CreateLogger<ConversationTracker>();

    readonly IConversationRepository _conversationRepository;
    readonly IConversationPublisher _conversationPublisher;

    public ConversationTracker(
        IConversationRepository conversationRepository,
        IConversationPublisher conversationPublisher)
    {
        _conversationRepository = conversationRepository;
        _conversationPublisher = conversationPublisher;
    }

    /// <summary>
    /// Should <paramref name="message"/> be tracked?
    /// <list type="number">
    /// <item>Organization must have <see cref="PlanFeature.ConversationTracking"/>.</item>
    /// <item>Room must be persistent.</item>
    /// <item>Room must have tracking enabled unless <paramref name="force"/> is <see langword="true"/>.</item>
    /// <item>Message must not be in thread.</item>
    /// <item>Message must be from supportee unless <paramref name="force"/> is <see langword="true"/>.</item>
    /// </list>
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="force">Create conversation from messages for any valid room.</param>
    public static bool ShouldTrackConversation(
        ConversationMessageContext message,
        bool force = false)
    {
        // Suppress diagnostic logs unless live
        var log = message.IsLive ? Log : NullLogger<ConversationTracker>.Instance;

        if (!message.Organization.HasPlanFeature(PlanFeature.ConversationTracking) || !message.Room.Persistent)
        {
            // This will be the case we don't log for.
            // It could get quite noisy and we're pretty sure this logic is sound.
            return false;
        }

        if (!force && !message.Room.ManagedConversationsEnabled)
        {
            log.DiscardingMessageFromUntrackedRoom();
            return false;
        }

        log.ReceivedConversationRelatedMessage();

        if (force)
        {
            return true;
        }

        if (message.IsInThread)
        {
            // Don't create new conversations for in-thread messages.
            // This prevents creating conversations in random old threads that existed prior to enabling conversation tracking.
            log.DiscardingThreadReply();
            return false;
        }

        // Don't create conversations for messages from home users
        if (!IsSupportee(message.From, message.Room))
        {
            log.DiscardingMessageFromNonSupportee(message.From.User.PlatformUserId, message.Organization.PlatformId);
            return false;
        }

        return true;
    }

    public Task<Conversation?> TryCreateNewConversationAsync(
        ConversationMessage message,
        DateTime utcNow,
        ConversationMatchAIResult? conversationMatchAIResult = null)
        => TryCreateNewConversationAsync(
            message,
            utcNow,
            conversationMatchAIResult,
            force: false);

    // Public to avoid test churn; interface exposes simpler overload
    public async Task<Conversation?> TryCreateNewConversationAsync(
        ConversationMessage message,
        DateTime utcNow,
        ConversationMatchAIResult? conversationMatchAIResult,
        bool force)
    {
        if (!ShouldTrackConversation(message, force))
        {
            return null;
        }

        var messagePostedEvent = new MessagePostedEvent
        {
            MessageId = message.MessageId,
            MessageUrl = message.GetMessageUrl(),
            Metadata = new MessagePostedMetadata
            {
                Categories = message.Categories,
                Text = message.Text,
                SensitiveValues = message.SensitiveValues,
                ConversationMatchAIResult = conversationMatchAIResult,
            }.ToJson()
        };

        var initialState = message.Room.ManagedConversationsEnabled
            ? ConversationState.New
            : ConversationState.Hidden;

        // Create a new conversation
        var convo = await _conversationRepository.CreateAsync(
            message.Room,
            messagePostedEvent,
            GetTitle(message),
            message.From,
            message.UtcTimestamp,
            message.IsLive ? null : utcNow,
            initialState);

        // We have to create a scope here because MetaBot won't have created a Conversation scope yet because there
        // was no conversation at the time it saw the message.
        using var scope = Log.BeginNewConversation(convo.Id);

        Log.NewConversationCreated(message.MessageId, initialState);

        await _conversationPublisher.PublishNewConversationAsync(convo, message, messagePostedEvent);

        return convo;
    }

    public async Task UpdateConversationAsync(Conversation conversation, ConversationMessage message)
    {
        // We don't need to create a conversation scope here, because the caller (MetaBot) already created one
        // when it got a non-null response from `TryIdentifyConversationAsync`.

        // Don't create conversations unless we have a persistent room, and a message ID
        if (!message.Organization.HasPlanFeature(PlanFeature.ConversationTracking)
            || message.Room is not { Persistent: true })
        {
            return;
        }

        Log.ReceivedConversationRelatedMessage();

        var messageUrl = message.GetMessageUrl();
        var messagePostedEvent = new MessagePostedEvent
        {
            MessageId = message.MessageId,
            MessageUrl = messageUrl,
            Metadata = new MessagePostedMetadata
            {
                Categories = message.Categories,
                Text = new(message.Text),
                SensitiveValues = message.SensitiveValues,
                ConversationMatchAIResult = message.MessageContext?.ConversationMatch?.Result,
            }.ToJson(),
        };

        await _conversationRepository.UpdateForNewMessageAsync(
            conversation,
            messagePostedEvent,
            message,
            IsSupportee(message.From, message.Room));
    }

    public async Task<Conversation?> CreateConversationAsync(
        IReadOnlyList<ConversationMessage> messages,
        Member actor,
        DateTime utcTimestamp)
    {
        if (messages.Count == 0)
        {
            throw new InvalidOperationException("Can’t create a conversation without messages!");
        }

        // First, make sure there isn't already a conversation
        var conversation = await _conversationRepository.GetConversationByThreadIdAsync(
            messages[0].MessageId,
            messages[0].Room,
            followHubThread: true);
        if (conversation is not null)
        {
            // It's possible something else created the conversation. If so, we'll just return it.
            return conversation;
        }

        // TODO: We _could_ use a transaction here, but I'm not sure it's necessary.
        // There's a brief race against the FR notification job that occurs between when we start creating the conversation and when we close it.
        // If the job runs, it might see the conversation and try to notify on it.
        // It's not the end of the world if that happens though, so let's not worry about it for now and see if it becomes a problem.

        var convo = await TryCreateNewConversationAsync(
            messages[0],
            utcTimestamp,
            conversationMatchAIResult: null,
            force: true);
        if (convo is null)
        {
            return null;
        }

        foreach (var message in messages.Skip(1))
        {
            await UpdateConversationAsync(convo, message);
        }

        // Record an import event and close the conversation.
        // We mark imported conversations as closed so that notifications don't fire unless someone posts.
        await _conversationRepository.AddTimelineEventAsync(convo, actor, utcTimestamp, new SlackImportEvent());
        if (convo.State is not ConversationState.Hidden)
        {
            await _conversationRepository.CloseAsync(convo, actor, utcTimestamp, "imported");
        }

        return convo;
    }

    /// <summary>
    /// Determines if the specified user is a "supportee" member, that is someone who can create conversations.
    /// Usually this is a member of any organization OTHER than the one that owns the room in which the message
    /// was received, or Channel Guests. But for community rooms, it's anyone who is not an Agent.
    /// </summary>
    /// <param name="member">The <see cref="Member"/> that posted the message.</param>
    /// <param name="room">The room the message was posted in.</param>
    /// <returns>A boolean indicating if the member is a "supportee" member.</returns>
    public static bool IsSupportee(Member member, Room room) =>
        IsSupportee(
            isExternalOrg: room.OrganizationId != member.OrganizationId,
            member.IsGuest,
            isCommunityRoom: room.Settings?.IsCommunityRoom is true,
            member.IsAgent());

    static bool IsSupportee(bool isExternalOrg, bool isGuest, bool isCommunityRoom, bool isAgent) =>
        isExternalOrg || isGuest || (isCommunityRoom && !isAgent);

    static string GetTitle(ConversationMessage message) =>
        message.Text;
}

static partial class ConversationTrackerLoggingExtensions
{
    static readonly Func<ILogger, int, IDisposable?> TrackingConversationScope =
        LoggerMessage.DefineScope<int>("Conversation: {ConversationId}");

    public static IDisposable? BeginNewConversation(this ILogger<ConversationTracker> logger, int conversationId)
        => TrackingConversationScope(logger, conversationId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "New Conversation created from message '{MessageId}' with state {ConversationState}")]
    public static partial void NewConversationCreated(this ILogger<ConversationTracker> logger, string messageId, ConversationState conversationState);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Received conversation-related message.")]
    public static partial void ReceivedConversationRelatedMessage(this ILogger<ConversationTracker> logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Discarding message from user '{PlatformUserId}' who is not considered a supportee in '{PlatformTeamId}'.")]
    public static partial void DiscardingMessageFromNonSupportee(this ILogger<ConversationTracker> logger, string platformUserId, string platformTeamId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Discarding thread reply")]
    public static partial void DiscardingThreadReply(this ILogger<ConversationTracker> logger);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Discarding message from untracked room")]
    public static partial void DiscardingMessageFromUntrackedRoom(this ILogger<ConversationTracker> logger);
}

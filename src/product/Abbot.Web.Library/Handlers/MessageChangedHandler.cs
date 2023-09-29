using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Events;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles <c>message_deleted</c> and <c>message_changed</c> events.
/// </summary>
public class MessageChangedHandler : IPayloadHandler<MessageDeletedEvent>, IPayloadHandler<MessageChangedEvent>
{
    static readonly ILogger<MessageChangedHandler> Log = ApplicationLoggerFactory.CreateLogger<MessageChangedHandler>();

    readonly IConversationRepository _conversationRepository;
    readonly IAnnouncementsRepository _announcementsRepository;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public MessageChangedHandler(
        IConversationRepository conversationRepository,
        IAnnouncementsRepository announcementsRepository,
        IAnalyticsClient analyticsClient,
        IClock clock)
    {
        _conversationRepository = conversationRepository;
        _announcementsRepository = announcementsRepository;
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<MessageDeletedEvent> platformEvent)
    {
        var deletedMessageId = platformEvent.Payload.DeletedTimestamp;
        await HandleDeletedMessageAsync(platformEvent, deletedMessageId);
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<MessageChangedEvent> platformEvent)
    {
        if (platformEvent.Payload.Message.IsDeleted())
        {
            var deletedMessageId = platformEvent.Payload.Message.Timestamp;
            await HandleDeletedMessageAsync(platformEvent, deletedMessageId.Require());
            return;
        }

        if (platformEvent.Payload is { Channel: { } channel, Message.Timestamp: { } messageId })
        {
            // Check to see if there's an announcement for this message
            var announcement = await _announcementsRepository.GetAnnouncementFromMessageAsync(
                channel,
                messageId,
                platformEvent.Organization);

            if (announcement is null)
            {
                return;
            }

            if (announcement.DateStartedUtc is not null)
            {
                bool isCurrentlyBeingSent = announcement.DateCompletedUtc is null;
                string fallbackText = isCurrentlyBeingSent
                    ? "You edited an announcement message that is currently being sent."
                    : "You edited an announcement message that has already been sent.";

                // This isn't ideal, but let's see if this actually gets usage and actually happens before we spend
                // a lot of time on making this more automatic.
                var text = $"{fallbackText} To update the sent messages with this change, click \"Update Sent Messages\""
                    + (isCurrentlyBeingSent
                        ? " when it is done being sent."
                        : ".");

                await platformEvent.Responder.SendEphemeralActivityAsync(
                    platformEvent.From.User.PlatformUserId,
                    fallbackText,
                    new RoomMessageTarget(channel),
                    new Section(
                        text,
                        new ButtonElement("Update Sent Messages", "update-sent-messages")
                        {
                            Style = ButtonStyle.Primary,
                            ActionId = $"{announcement.Id}",
                            Confirm = new ConfirmationDialog(
                                "Update Sent Messages",
                                new MrkdwnText("Are you sure you want to update all sent announcement messages with this new message?"),
                                "Yes, do it!",
                                "Never Mind")
                        })
                    {
                        BlockId = InteractionCallbackInfo.For<AnnouncementHandler>(),
                    });
            }
            else
            {
                string fallbackText = "You edited an announcement message that hasn't been posted to any rooms yet. Your changes will automatically be applied when the announcement is sent.";
                await platformEvent.Responder.SendEphemeralActivityAsync(
                    platformEvent.From.User.PlatformUserId,
                    fallbackText,
                    new RoomMessageTarget(channel),
                    new Section(
                        fallbackText));
            }

            _analyticsClient.Track(
                "Message Edited",
                AnalyticsFeature.Announcements,
                platformEvent.From,
                platformEvent.Organization,
                new()
                {
                    ["status"] = announcement switch
                    {
                        { DateStartedUtc: null } => "not-started",
                        { DateStartedUtc: { }, DateCompletedUtc: null } => "in-progress",
                        { DateStartedUtc: { }, DateCompletedUtc: { } } => "completed",
                    }
                });
        }
    }

    async Task HandleDeletedMessageAsync(IPlatformEvent platformEvent, string deletedMessageId)
    {
        Log.MethodEntered(typeof(MessageChangedHandler), nameof(HandleDeletedMessageAsync),
            $"Message {deletedMessageId} deleted");

        if (platformEvent is { Room: { } room })
        {
            var conversation = await _conversationRepository.GetConversationByThreadIdAsync(
                deletedMessageId,
                room);

            if (conversation is not null)
            {
                // This will stop tracking the conversation.
                await _conversationRepository.ArchiveAsync(conversation, platformEvent.From, _clock.UtcNow);
                Log.ConversationArchived(conversation.Id, deletedMessageId);
            }
        }
    }
}

public static partial class MessageChangedHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Conversation {ConversationId} Archived because message {DeletedMessageId} was deleted.")]
    public static partial void ConversationArchived(this ILogger logger, int conversationId, string deletedMessageId);
}

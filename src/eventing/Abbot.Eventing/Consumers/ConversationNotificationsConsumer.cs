using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Eventing;

/// <summary>
/// A consumer that handles reporting all kinds of notifications to Hub threads.
/// </summary>
/// <remarks>
/// This handles things like tickets added to a conversation, etc. It does NOT handle conversation overdue
/// warnings.
/// </remarks>
public class ConversationNotificationsConsumer : IConsumer<PublishConversationNotification>
{
    const string IndividualDMDelivery = "IndividualDM";
    const string HubDelivery = "Hub";

    readonly ISlackApiClient _slackApiClient;
    readonly IConversationRepository _conversationRepository;
    readonly IHubRepository _hubRepository;
    readonly ILogger<ConversationNotificationsConsumer> _logger;

    public ConversationNotificationsConsumer(
        ISlackApiClient slackApiClient,
        IConversationRepository conversationRepository,
        IHubRepository hubRepository,
        ILogger<ConversationNotificationsConsumer> logger)
    {
        _slackApiClient = slackApiClient;
        _conversationRepository = conversationRepository;
        _hubRepository = hubRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PublishConversationNotification> context)
    {
        var organization = context.GetPayload<Organization>();

        var conversation = await _conversationRepository.GetConversationAsync(context.Message.ConversationId);
        if (conversation is null)
        {
            _logger.EntityNotFound(context.Message.ConversationId);
            return;
        }
        conversation.RequireParent(organization);
        using var convoScopes = _logger.BeginConversationRoomAndHubScopes(conversation);

        if (!organization.TryGetUnprotectedApiToken(out var slackToken))
        {
            _logger.OrganizationHasNoSlackApiToken();
            return;
        }

        Expect.True(
            context.Message.Notification.Type is not NotificationType.Warning && context.Message.Notification.Type is not NotificationType.Deadline,
            "We no longer publish Conversation Warnings or Deadlines here. Instead, they're sent by the recurring job.");

        if (conversation is { HubId: not null, HubThreadId: not null })
        {
            await PublishNotificationToHubAsync(
                slackToken,
                conversation,
                new(conversation.HubId.Value),
                context.Message.Notification,
                context.Message.Broadcast);
        }
        else
        {
            foreach (var group in context.Message.Notification.MentionGroups)
            {
                await PublishNotificationAsDirectMessageAsync(
                    slackToken,
                    context.Message.Notification,
                    group);
            }
        }
    }

    async Task PublishNotificationAsDirectMessageAsync(
        string apiToken,
        ConversationNotification notification,
        MentionGroup group)
    {
        var (text, section) = FormatNotification(notification);
        var blocks = new List<ILayoutBlock>
        {
            section,
            new Context(FormatMentionGroup(group)),
        };
        foreach (var mentionId in group.MentionIds)
        {
            var message = new MessageRequest(mentionId, text)
            {
                Blocks = blocks,
            };

            var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, message);

            if (response.Ok)
            {
                _logger.PublishedNotification(
                    notification.Type,
                    IndividualDMDelivery,
                    group.RecipientType,
                    response.Timestamp);
            }
            else
            {
                _logger.ErrorPublishingNotification(
                    notification.Type,
                    IndividualDMDelivery,
                    group.RecipientType,
                    response.Error);
            }
        }
    }

    async Task PublishNotificationToHubAsync(
        string apiToken,
        Conversation conversation,
        Id<Hub> hubId,
        ConversationNotification notification,
        bool broadcast)
    {
        // The Hub property on Conversation doesn't Include the Room navigation property.
        // So we need to pull the Hub from the DB separately.
        var hub = await _hubRepository.GetHubByIdAsync(hubId);
        if (hub is null)
        {
            _logger.EntityNotFound(hubId);
            return;
        }

        var (text, section) = FormatNotification(notification);
        var blocks = new List<ILayoutBlock>
        {
            section
        };

        foreach (var group in notification.MentionGroups)
        {
            blocks.Add(new Context(FormatMentionGroup(group)));
        }

        var messageRequest = new MessageRequest(hub.Room.PlatformRoomId, text)
        {
            ThreadTs = conversation.HubThreadId,
            Blocks = blocks,
            ReplyBroadcast = broadcast
        };

        var resp = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);
        if (resp.Ok)
        {
            _logger.PublishedNotification(notification.Type, HubDelivery, NotificationRecipientType.All, resp.Timestamp);
        }
        else
        {
            _logger.ErrorPublishingNotification(notification.Type, HubDelivery, NotificationRecipientType.All, resp.Error);
        }
    }

    static (string FallbackText, Section Body) FormatNotification(ConversationNotification notification)
    {
        var mrkdwn = $"{notification.Icon} *{notification.Headline}*: {notification.Message}";
        var text = $"{notification.Icon} {notification.Headline}: {notification.Message}";

        var section = new Section(new MrkdwnText(mrkdwn));
        return (text, section);
    }

    static string FormatMentionGroup(MentionGroup group)
    {
        var header = group.RecipientType switch
        {
            NotificationRecipientType.Actor => "Actor",
            NotificationRecipientType.Assignee => "Assigned to this conversation",
            NotificationRecipientType.EscalationResponder => "Escalation team",
            NotificationRecipientType.FirstResponder => "First responders",
            var x => throw new ArgumentOutOfRangeException(nameof(group), $"Unknown {nameof(group.RecipientType)}: {x}"),
        };
        return $"*{header}*: {string.Join(" ", group.MentionIds.Select(SlackFormatter.UserMentionSyntax))}";
    }
}

static partial class SyncConversationWithHubConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Published {NotificationType} {NotificationDelivery} Notification to {RecipientType}: {SlackTimestamp}")]
    public static partial void PublishedNotification(
        this ILogger<ConversationNotificationsConsumer> logger,
        NotificationType notificationType,
        string notificationDelivery,
        NotificationRecipientType recipientType,
        string slackTimestamp);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Failed to Publish {NotificationType} {NotificationDelivery} Notification to {RecipientType}: {SlackError}")]
    public static partial void ErrorPublishingNotification(
        this ILogger<ConversationNotificationsConsumer> logger,
        NotificationType notificationType,
        string notificationDelivery,
        NotificationRecipientType recipientType,
        string slackError);
}

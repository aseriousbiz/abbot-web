using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Eventing;

/// <summary>
/// A consumer that handles reporting all kinds of notifications for a room.
/// </summary>
public class RoomNotificationsConsumer : IConsumer<PublishRoomNotification>
{
    const string GroupDMDelivery = "GroupDM";
    const string HubDelivery = "Hub";

    readonly ISlackApiClient _slackApiClient;
    readonly IRoomRepository _roomRepository;
    readonly IUserRepository _userRepository;
    readonly IHubRepository _hubRepository;
    readonly ILogger<RoomNotificationsConsumer> _logger;

    public RoomNotificationsConsumer(
        ISlackApiClient slackApiClient,
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        IHubRepository hubRepository,
        ILogger<RoomNotificationsConsumer> logger)
    {
        _slackApiClient = slackApiClient;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
        _hubRepository = hubRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PublishRoomNotification> context)
    {
        var organization = context.GetPayload<Organization>();

        var roomId = context.Message.RoomId;
        var room = await _roomRepository.GetRoomAsync(roomId);
        if (room is null && roomId is not null)
        {
            _logger.EntityNotFound(roomId.Value);
            return;
        }

        using var roomScope = _logger.BeginRoomScope(room);
        room?.RequireParent(organization);

        var hub = await GetHubForRoomAsync(room, organization);
        using var hubScope = _logger.BeginHubScope(hub);

        if (!organization.TryGetUnprotectedApiToken(out var slackToken))
        {
            _logger.OrganizationHasNoSlackApiToken();
            return;
        }

        var notification = context.Message.Notification;

        // Get responders to notify.
        var responders = await GetResponderGroup(organization, room, notification.Escalation);

        if (hub is not null)
        {
            await PublishNotificationToHubAsync(
                slackToken,
                hub,
                notification,
                responders);
        }
        else
        {
            await PublishNotificationAsDirectMessageAsync(
                slackToken,
                notification,
                responders);
        }
    }

    async Task<Hub?> GetHubForRoomAsync(Room? room, Organization organization)
    {
        // Is this room itself a hub?
        if (room is not null && await _hubRepository.GetHubAsync(room) is { } thisRoomHub)
        {
            return thisRoomHub;
        }

        // Ok, resolve the hub for this room
        var hubId = (Id<Hub>?)room?.HubId ?? organization.Settings.DefaultHubId;
        var hub = hubId is null ? null : await _hubRepository.GetHubByIdAsync(hubId.Value);
        return hub;
    }

    async Task<ResponderGroup> GetResponderGroup(Organization organization, Room? room, bool escalation)
    {
        IReadOnlyList<IChatUser> responders;
        if (escalation)
        {
            responders = room.GetEscalationResponders()
                .Select(r => r.ToPlatformUser())
                .ToReadOnlyList();
            if (!responders.Any())
            {
                responders = (await _userRepository.GetDefaultEscalationRespondersAsync(organization))
                    .Select(r => r.ToPlatformUser()).ToReadOnlyList();
            }

            return new ResponderGroup(RoomRole.EscalationResponder, responders);
        }
        responders = room.GetFirstResponders()
            .Select(r => r.ToPlatformUser())
            .ToReadOnlyList();
        if (!responders.Any())
        {
            responders = (await _userRepository.GetDefaultFirstRespondersAsync(organization))
                .Select(r => r.ToPlatformUser())
                .ToReadOnlyList();
        }

        return new ResponderGroup(RoomRole.FirstResponder, responders);
    }

    async Task PublishNotificationAsDirectMessageAsync(
        string apiToken,
        RoomNotification notification,
        ResponderGroup responders)
    {
        var mentionIds = responders.MentionIds;
        var createConvoResponse = await _slackApiClient.Conversations.OpenDirectMessageAsync(
            apiToken,
            mentionIds);

        if (!createConvoResponse.Ok)
        {
            _logger.ErrorCreatingGroupDM(string.Join(",", mentionIds), createConvoResponse.Error);
            return;
        }

        var address = createConvoResponse.Body.Id;

        var (text, section) = FormatNotification(notification);
        var blocks = new List<ILayoutBlock>
        {
            section,
            new Context(FormatMentionGroup(responders)),
        };

        var message = new MessageRequest(address, text)
        {
            Blocks = blocks,
        };

        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, message);
        if (response.Ok)
        {
            _logger.PublishedNotification(GroupDMDelivery, responders.RoomRole, response.Timestamp);
        }
        else
        {
            _logger.ErrorPublishingNotification(GroupDMDelivery, responders.RoomRole, response.Error);
        }
    }

    async Task PublishNotificationToHubAsync(
        string apiToken,
        Hub hub,
        RoomNotification notification,
        ResponderGroup responders)
    {
        var (text, section) = FormatNotification(notification);
        var blocks = new List<ILayoutBlock>
        {
            section,
        };

        if (responders.MentionIds.Count > 0)
        {
            blocks.Add(new Context(FormatMentionGroup(responders)));
        }

        var messageRequest = new MessageRequest(hub.Room.PlatformRoomId, text)
        {
            Blocks = blocks
        };

        var resp = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);
        if (resp.Ok)
        {
            _logger.PublishedNotification(HubDelivery, responders.RoomRole, resp.Timestamp);
        }
        else
        {
            _logger.ErrorPublishingNotification(HubDelivery, responders.RoomRole, resp.Error);
        }
    }

    static (string FallbackText, Section Body) FormatNotification(RoomNotification notification)
    {
        var mrkdwn = $"{notification.Icon} *{notification.Headline}*: {notification.Message}";
        var text = $"{notification.Icon} {notification.Headline}: {notification.Message}";

        var section = new Section(new MrkdwnText(mrkdwn));
        return (text, section);
    }

    static string FormatMentionGroup(ResponderGroup group)
    {
        var header = group.RoomRole switch
        {
            RoomRole.EscalationResponder => "Escalation team",
            RoomRole.FirstResponder => "First responders",
            var x => throw new ArgumentOutOfRangeException(nameof(group), $"Unknown {nameof(group.RoomRole)}: {x}"),
        };

        var mentions = group.MentionIds.Select(SlackFormatter.UserMentionSyntax);
        return $"*{header}*: {string.Join(" ", mentions)}";
    }
}

static partial class RoomNotificationsConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Published {NotificationDelivery} Notification to {RoomRole}: {SlackTimestamp}")]
    public static partial void PublishedNotification(
        this ILogger<RoomNotificationsConsumer> logger,
        string notificationDelivery,
        RoomRole roomRole,
        string slackTimestamp);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Failed to Publish {NotificationDelivery} Notification to {RoomRole}: {SlackError}")]
    public static partial void ErrorPublishingNotification(
        this ILogger<RoomNotificationsConsumer> logger,
        string notificationDelivery,
        RoomRole roomRole,
        string slackError);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed to create group DM for {UserList}: {SlackError}")]
    public static partial void ErrorCreatingGroupDM(
        this ILogger<RoomNotificationsConsumer> logger,
        string? userList,
        string slackError);
}

using System.Collections.Generic;
using System.Linq;
using MassTransit;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Integrations;

public class TicketNotificationService
{
    readonly IUserRepository _userRepository;
    readonly IPublishEndpoint _publishEndpoint;

    public TicketNotificationService(
        IUserRepository userRepository,
        IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync(
        Conversation conversation,
        NotificationType notificationType,
        string headline,
        string message,
        Member actor)
    {
        var (nrt, aofr) = await AssigneesOrFirstRespondersAsync(conversation);
        var aofrGroup = new MentionGroup(nrt, aofr.Select(m => m.User.PlatformUserId).ToList());
        var notification = new ConversationNotification
        {
            Headline = headline,
            Icon = "🎫",
            Message = message,
            Type = notificationType,
            MentionGroups =
                {
                    aofrGroup,
                },
        };

        // Notify actor if not assignee/responder
        var actorPlatformUserId = actor.User.PlatformUserId;
        var room = conversation.Room;
        if (!aofrGroup.MentionIds.Contains(actorPlatformUserId)
            && !ConversationTracker.IsSupportee(actor, room))
        {
            notification.MentionGroups.Add(
                new(NotificationRecipientType.Actor, new[] { actorPlatformUserId }));
        }

        await _publishEndpoint.Publish(new PublishConversationNotification
        {
            OrganizationId = room.Organization,
            ConversationId = conversation,
            Notification = notification,
            Broadcast = false,
        });
    }

    async Task<(NotificationRecipientType, IReadOnlyList<Member>)> AssigneesOrFirstRespondersAsync(Conversation conversation)
    {
        var assignees = conversation.Require().Assignees;
        if (assignees is not [])
        {
            return (NotificationRecipientType.Assignee, assignees);
        }

        Expect.NotNull(conversation.Room.Assignments);

        var room = conversation.Room;
        var roomResponders = room.GetFirstResponders().ToList();
        if (roomResponders is not [])
        {
            return (NotificationRecipientType.FirstResponder, roomResponders);
        }

        var defaultResponders = await _userRepository.GetDefaultFirstRespondersAsync(room.Organization);
        return (NotificationRecipientType.FirstResponder, defaultResponders);
    }
}

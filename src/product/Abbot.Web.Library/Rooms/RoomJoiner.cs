using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Slack;

namespace Serious.Abbot.Rooms;

public interface IRoomJoiner
{
    Task<ConversationInfoResponse> JoinAsync(Room room, Member actor);
}

public class RoomJoiner : IRoomJoiner
{
    private readonly ISlackApiClient _slackApiClient;
    private readonly IRoomRepository _roomRepository;
    readonly IAuditLog _auditLog;

    public RoomJoiner(ISlackApiClient slackApiClient, IRoomRepository roomRepository,
        IAuditLog auditLog)
    {
        _slackApiClient = slackApiClient;
        _roomRepository = roomRepository;
        _auditLog = auditLog;
    }

    public async Task<ConversationInfoResponse> JoinAsync(Room room, Member actor)
    {
        var apiToken = room.Organization.RequireAndRevealApiToken();
        var result = await _slackApiClient.Conversations.JoinConversationAsync(
            apiToken,
            new ConversationJoinRequest(room.PlatformRoomId));

        if (result.Ok)
        {
            // The RoomMembershipEventPayload handles updating the BotIsMember property in response to
            // the slack event when Abbot is added to the room. However, if this API is successful, we
            // want to make the update here so that the UI is updated immediately. Otherwise it's confusing.
            room.BotIsMember = true;

            await _auditLog.LogBotInvitedAsync(actor, room);
        }
        else
        {
            switch (result.Error)
            {
                case "channel_not_found":
                    room.ManagedConversationsEnabled = false;
                    room.Deleted = true;
                    break;

                case "is_archived":
                    room.Archived = true;
                    break;

                case "method_not_supported_for_channel_type":
                    room.ManagedConversationsEnabled = false;
                    room.Archived = true;
                    break;
            }
        }
        await _roomRepository.UpdateAsync(room);
        return result;
    }
}

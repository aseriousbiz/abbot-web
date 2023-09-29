using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;

namespace Serious.Abbot.Pages.Staff.Organizations.Import;

public record ChannelMembershipLookupTable(int RoomId, HashSet<string> SlackUserIds);

public class MemberLookupTable
{
    readonly IReadOnlyDictionary<string, Member> _membersByEmail;
    readonly IReadOnlyDictionary<int, HashSet<string>> _lookupTableByRoomId;

    public MemberLookupTable(IEnumerable<Member> members, IEnumerable<ChannelMembershipLookupTable> channelMembershipLookupTables)
    {
        _membersByEmail = members.ToDictionary(m => m.User.Email!);
        _lookupTableByRoomId = channelMembershipLookupTables.ToDictionary(t => t.RoomId, t => t.SlackUserIds);
    }

    Member? LookupMemberByEmail(string email) => _membersByEmail.TryGetValue(email, out var member)
        ? member
        : null;

    bool IsMemberOfRoom(Member member, Room room) =>
        _lookupTableByRoomId.TryGetValue(room.Id, out var slackUserIds)
        && slackUserIds.Contains(member.User.PlatformUserId);

    public LookupResult<Member> LookupMemberForRoom(string email, Room? room)
    {
        string statusMessage = "";
        var errorMessages = new List<string>();
        var member = LookupMemberByEmail(email);

        if (member is not null && room is not null)
        {
            var isMemberOfRoom = IsMemberOfRoom(member, room);

            if (member.User.NameIdentifier is not { Length: > 0 })
            {
                errorMessages.Add("has not logged into ab.bot yet");
            }

            if (!member.IsInRole(Roles.Agent))
            {
                errorMessages.Add("is not an agent");
            }

            if (!isMemberOfRoom)
            {
                errorMessages.Add("not a member of this room");
            }
            else
            {
                bool isFirstResponder = member
                    .RoomAssignments
                    .Any(ra => ra.RoomId == room.Id && ra.Role == RoomRole.FirstResponder);

                if (isFirstResponder)
                {
                    statusMessage = "Is already a first responder of this room";
                }
            }
        }

        var errorMessage = errorMessages.Humanize();

        return member is not null
            ? new LookupResult<Member>(email, member, errorMessage, statusMessage)
            : new LookupResult<Member>(email, null, "Could not find a user with that email address");
    }
}

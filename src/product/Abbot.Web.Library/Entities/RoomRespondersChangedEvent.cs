using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when the set of first responders or escalation responders changes for a room.
/// </summary>
public class RoomRespondersChangedEvent : AdminAuditEvent
{
    [NotMapped]
    public override bool HasDetails => true;
}

/// <summary>
/// Information about the responders added and removed from a room.
/// </summary>
/// <param name="RoomRole">The room role for the responders.</param>
/// <param name="AddedResponders">The responders added to the <see cref="RoomRole"/> for this room.</param>
/// <param name="RemovedResponders">The responders removed from the <see cref="RoomRole"/> for this room.</param>
/// <param name="RespondersCount">The count of custom responders set for the room.</param>
public record RespondersInfo(
    RoomRole RoomRole,
    IReadOnlyList<ResponderInfo> AddedResponders,
    IReadOnlyList<ResponderInfo> RemovedResponders,
    int RespondersCount);

/// <summary>
/// Information about a responder.
/// </summary>
/// <param name="MemberId">The database Id of the first responder in case we want to look them up.</param>
/// <param name="Name">The name of the first responder.</param>
/// <param name="PlatformUserId">The platform-specific Id of the user.</param>
public record ResponderInfo(int MemberId, string Name, string PlatformUserId)
{
    public static ResponderInfo FromMember(Member member)
    {
        return new ResponderInfo(member.Id, member.User.DisplayName, member.User.PlatformUserId);
    }
}

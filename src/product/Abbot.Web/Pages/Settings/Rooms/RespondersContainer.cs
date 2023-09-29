using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Settings.Rooms;

/// <summary>
/// Model used to render UI to select responders.
/// </summary>
/// <param name="Members">The members of the room role.</param>
/// <param name="Agents">All agents for the organization.</param>
/// <param name="CurrentMember">The current member.</param>
/// <param name="Organization">The current organization.</param>
/// <param name="RoomRole">The room role such as "first-responders" or "escalation-responders".</param>
/// <param name="RoleMemberDescription">A human friendly description of the room role.</param>
public record RespondersContainer(
    IReadOnlyList<Member> Members,
    IReadOnlyList<Member> Agents,
    Member CurrentMember,
    Entities.Organization Organization,
    RoomRole RoomRole,
    string RoleMemberDescription);


/// <summary>
/// Model used to render UI to select assignee.
/// </summary>
/// <param name="Agents">All agents for the organization.</param>
/// <param name="CurrentAssignee">The current assigned agent.</param>
/// <param name="Organization">The current organization.</param>
public record AssigneeContainer(
    IReadOnlyList<Member> Agents,
    Member? CurrentAssignee,
    Entities.Organization Organization);

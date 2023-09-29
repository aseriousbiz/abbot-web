using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event when a user changes the response times for a room.
/// </summary>
public class RoomResponseTimesChangedEvent : AdminAuditEvent
{
    [NotMapped]
    public override bool HasDetails => true;
}

/// <summary>
/// Information about the response time changes.
/// </summary>
/// <param name="OldTarget">The old target response time.</param>
/// <param name="OldDeadline">The old deadline response time.</param>
/// <param name="NewTarget">The new target response time.</param>
/// <param name="NewDeadline">The new deadline response time.</param>
/// <param name="OrganizationDefaultTarget">The organization's default target response time at the time of the change.</param>
/// <param name="OrganizationDefaultDeadline">The organization's default deadline response time at the time of the change.</param>
public record ResponseTimeInfo(
    TimeSpan? OldTarget,
    TimeSpan? OldDeadline,
    TimeSpan? NewTarget,
    TimeSpan? NewDeadline,
    TimeSpan? OrganizationDefaultTarget,
    TimeSpan? OrganizationDefaultDeadline);

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when a staff member views the code for an Audit Event.
/// </summary>
public class StaffViewedCodeAuditEvent : StaffAuditEvent
{
    /// <summary>
    /// The Identifier for the viewed audit event.
    /// </summary>
    [Column("ViewedIdentifier")]
    public Guid ViewedIdentifier { get; init; }

    [NotMapped]
    public override bool HasDetails => true;
}

public class StaffViewedSlackEventContent : StaffAuditEvent
{
    /// <summary>
    /// The Id of the viewed slack event.
    /// </summary>
    /// <remarks>
    /// A while ago I tried to save a column by using the `RoomId` column to store this value. However, that
    /// turned out to be a bad idea because later, I moved the `RoomId` to <see cref="LegacyAuditEvent"/>. This broke
    /// saving a <see cref="StaffViewedSlackEventContent"/> as it appeared we were trying to have two RoomId columns.
    /// Instead, we'll continue to use the regular `RoomId` column and just make this a computed property.
    /// </remarks>
    [NotMapped]
    public string EventId
    {
        get => RoomId ?? "Unknown";
        init => RoomId = value;
    }

    [NotMapped]
    public override bool HasDetails => true;
}

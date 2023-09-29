using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Base class for audit events that log staff user actions.
/// </summary>
public class StaffAuditEvent : LegacyAuditEvent
{
    /// <summary>
    /// The reason the staff member viewed the audit event.
    /// </summary>
    [Column("Reason")]
    public string Reason { get; init; } = null!;

    /// <summary>
    /// Any code (such as a JSON string) that was associated with the event.
    /// </summary>
    [Column(nameof(Code))]
    public string? Code { get; init; }

    [NotMapped]
    public override bool HasDetails => true;
}

using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Base class for when a trigger is created, changed, or removed.
/// </summary>
public abstract class TriggerChangeEvent : SkillAuditEvent
{
    /// <summary>
    /// The description of the trigger.
    /// </summary>
    [Column("ChangeDescription")]
    public string? TriggerDescription { get; set; }

    [NotMapped]
    public override bool HasDetails => true;
}

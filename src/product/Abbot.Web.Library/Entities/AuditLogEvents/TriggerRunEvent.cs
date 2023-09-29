using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public abstract class TriggerRunEvent : SkillRunAuditEvent
{
    /// <summary>
    /// The description of the trigger.
    /// </summary>
    [Column("ChangeDescription")]
    public string? TriggerDescription { get; set; }
}

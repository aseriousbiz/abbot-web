using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public class SkillInfoChangedAuditEvent : SkillAuditEvent
{
    /// <summary>
    /// The type of change made.
    /// </summary>
    public string ChangeType { get; set; } = "Changed";

    /// <summary>
    /// A description of the change.
    /// </summary>
    [Column("ChangeDescription")]
    public string ChangeDescription { get; set; } = string.Empty;

    /// <summary>
    /// The new description, if it was changed.
    /// </summary>
    [Column("Code")] // Repurposing the column of a sibling hierarchy.
    public string? NewDescription { get; set; }

    /// <summary>
    /// The new usage, if it was changed.
    /// </summary>
    [Column("Arguments")]  // Repurposing the column of a sibling hierarchy.
    public string? NewUsage { get; set; }

    [NotMapped]
    public override bool HasDetails => true;
}

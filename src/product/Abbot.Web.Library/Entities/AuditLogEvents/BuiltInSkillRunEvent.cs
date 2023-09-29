using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// The event when a user runs a built-in skill.
/// </summary>
public class BuiltInSkillRunEvent : LegacyAuditEvent
{
    /// <summary>
    /// The name of the skill
    /// </summary>
    [Column(nameof(SkillName))]
    public string SkillName { get; set; } = null!;

    /// <summary>
    /// The arguments passed to the skill.
    /// </summary>
    [Column(nameof(Arguments))]
    public string Arguments { get; set; } = string.Empty;

    [NotMapped]
    public override bool HasDetails => true;
}

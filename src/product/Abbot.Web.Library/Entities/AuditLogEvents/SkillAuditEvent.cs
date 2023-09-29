using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised that pertains to a skill such as changing a skill.
/// </summary>
public class SkillAuditEvent : LegacyAuditEvent
{
    /// <summary>
    /// The Id of the skill this event is associated with.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// The name of the skill
    /// </summary>
    [Column(nameof(SkillName))]
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// The language of the skill
    /// </summary>
    public CodeLanguage Language { get; set; }
}

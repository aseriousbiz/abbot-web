using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public class SkillNotFoundEvent : LegacyAuditEvent
{
    /// <summary>
    /// The command the user attempted to run.
    /// </summary>
    public string Command { get; set; } = null!;

    /// <summary>
    /// What we responded with.
    /// </summary>
    [Column("Response")]
    public string Response { get; set; } = null!;

    /// <summary>
    /// The type of response sent to the user.
    /// </summary>
    public ResponseSource ResponseSource { get; set; }

    [NotMapped]
    public override bool HasDetails => true;
}

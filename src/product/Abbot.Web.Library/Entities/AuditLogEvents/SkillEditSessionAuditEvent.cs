using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an edit session. A session is a series of edits within a moving time window (say 10 minutes).
/// Rather than logging every single edit, we create a new session only when 10 minutes have passed between
/// edits.
/// </summary>
public class SkillEditSessionAuditEvent : SkillAuditEvent
{
    /// <summary>
    /// The Id of the first skill version in this edit session.
    /// </summary>
    [Column(nameof(FirstSkillVersionId))]
    public int? FirstSkillVersionId { get; set; }

    /// <summary>
    /// The resulting code from this edit session.
    /// </summary>
    [Column("Code")]
    public string? Code { get; set; }

    /// <summary>
    /// The number of edits made during the session.
    /// </summary>
    public int EditCount { get; set; }

    /// <summary>
    /// The date the last edit in this session occurred.
    /// </summary>
    public DateTime Modified { get; set; }

    [NotMapped]
    public override bool HasDetails => true;
}

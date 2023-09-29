using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Serious.Abbot.AI;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event specifically related to running a skill.
/// </summary>
[DebuggerDisplay("Skill Run: {Identifier} (Parent: {ParentIdentifier})")]
public class SkillRunAuditEvent : SkillAuditEvent
{
    /// <summary>
    /// The arguments passed to the skill.
    /// </summary>
    [Column(nameof(Arguments))]
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// The executable code for the skill.
    /// </summary>
    [Column(nameof(Code))]
    public string? Code { get; set; }

    /// <summary>
    /// The set of secrets in the skill at the time it was run, if any.
    /// </summary>
    [Column(nameof(Secrets))]
    public string? Secrets { get; set; }

    /// <summary>
    /// Description of the pattern that matched, if any.
    /// </summary>
    [Column(nameof(StaffViewedCodeAuditEvent.Reason))]
    public string? PatternDescription { get; set; }

    /// <summary>
    /// Name of the signal this skill was responding to, if any.
    /// </summary>
    public string? Signal { get; set; }

    [NotMapped]
    public override bool HasDetails => true;

    [NotMapped]
#pragma warning disable CA1044
    public new SkillRunProperties Properties
#pragma warning restore CA1044
    {
        set => base.Properties = value;
    }
}

public record SkillRunProperties
{
    /// <summary>
    /// Gets or inits the text of the command that was used to run this skill.
    /// </summary>
    public string CommandText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or inits the <see cref="ArgumentRecognitionResult"/> representing the results of performing argument recognition on the command text.
    /// </summary>
    public ArgumentRecognitionResult? ArgumentRecognitionResult { get; set; }
}

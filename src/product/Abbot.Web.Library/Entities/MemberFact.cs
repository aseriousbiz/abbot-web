using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A fact about the user stored via the who skill.
/// </summary>
public class MemberFact : TrackedEntityBase<MemberFact>, IAuditableEntity
{
    /// <summary>
    /// The content of the fact.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// The <see cref="Member"/> the fact is about.
    /// </summary>
    public Member Subject { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Member"/> the fact is about.
    /// </summary>
    public int SubjectId { get; set; }

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var preposition = auditOperation.GetPreposition();

        return new AuditEvent
        {
            Type = new("MemberFact", auditOperation),
            Description = $"{auditOperation} fact `{Content}` {preposition} the user `{Subject.DisplayName}` via the `who` skill."
        };
    }
}

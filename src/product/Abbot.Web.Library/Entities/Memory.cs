using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A stored Abbot memory. Memories are created with the `rem` skill.
/// </summary>
[SuppressMessage("Microsoft.Naming", "CA1724", Justification = "It's too late to change this.")]
public class Memory : TrackedEntityBase<Memory>, IOrganizationEntity, INamedEntity, IAuditableEntity
{
    /// <summary>
    /// The name of the memory.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The content of the memory.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// The id of the organization the memory belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The organization the memory belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var skill = auditOperation is AuditOperation.Removed ? "forget" : "rem";
        var verb = auditOperation == AuditOperation.Created ? "Added" : "Removed";

        var description = $"{verb} memory `{Name}` via the `{skill}` skill.";
        return new AuditEvent
        {
            Type = new("Memory", auditOperation),
            Description = description
        };
    }
}

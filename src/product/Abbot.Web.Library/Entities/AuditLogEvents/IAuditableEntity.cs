using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an entity where we log the creation, deletion, and changes to it.
/// </summary>
public interface IAuditableEntity : IEntity
{
    /// <summary>
    /// Creates an audit event of a type specific to the entity.
    /// </summary>
    /// <remarks>
    /// Implementations might not log every single type change. For example, when we apply an automated update such
    /// as setting <see cref="Announcement.ScheduledJobId"/>, we don't want a log entry.
    /// </remarks>
    /// <param name="auditOperation">The type of audit event.</param>
    /// <returns>An <see cref="AuditEventBase"/> for the specified event type. <c>nulL</c> if the event should not be created.</returns>
    AuditEventBase? CreateAuditEventInstance(AuditOperation auditOperation);
}

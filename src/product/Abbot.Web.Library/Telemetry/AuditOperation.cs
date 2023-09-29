namespace Serious.Abbot.Telemetry;

/// <summary>
/// The type of audit event.
/// </summary>
public enum AuditOperation
{
    /// <summary>
    /// The entity was created.
    /// </summary>
    Created,

    /// <summary>
    /// The entity was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The entity was changed.
    /// </summary>
    Changed
}

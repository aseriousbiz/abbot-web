using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// An entry in a <see cref="UserList"/>.
/// </summary>
public class UserListEntry : TrackedEntityBase<UserListEntry>, IAuditableEntity
{
    /// <summary>
    /// The Id of the parent list.
    /// </summary>
    public int ListId { get; set; }

    /// <summary>
    /// The parent <see cref="UserList"/>.
    /// </summary>
    public UserList List { get; set; } = null!;

    /// <summary>
    /// The content of the entry.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var preposition = auditOperation is AuditOperation.Created ? "to" : "from";
        var verb = auditOperation == AuditOperation.Created ? "Added" : "Removed";

        var description = $"{verb} `{Content}` {preposition} list `{List.Name}`.";
        return new AuditEvent
        {
            Type = new("UserListEntry", auditOperation),
            Description = description
        };
    }
}

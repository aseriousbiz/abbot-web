using System.Linq;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

public record AuditEventBuilder
{
    /// <summary>
    /// Gets or sets the type of this audit event.
    /// </summary>
    public required AuditEventType Type { get; init; }

    /// <summary>
    /// A sanitized description of the logged action.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The <see cref="Member"/> that initiated the logged action.
    /// This member may not come from the same organization as the event itself (such as when staff perform operations).
    /// </summary>
    public required Member Actor { get; init; }

    /// <summary>
    /// The <see cref="Organization"/> the audit event belongs to.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// A detailed description of the logged action.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or inits the properties associated with this audit event.
    /// </summary>
    public object Properties { get; init; } = new();

    /// <summary>
    /// Gets or inits the ID of the entity this audit event relates to.
    /// The entity type for this is determined by the subclass.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// If set to true, this event is visible to staff only.
    /// </summary>
    public bool StaffOnly { get; set; }

    /// <summary>
    /// Gets or inits a boolean indicating if a staff user performed this action.
    /// </summary>
    public bool StaffPerformed { get; init; }

    /// <summary>
    /// Gets or inits the reason given by a staff user for performing this action.
    /// </summary>
    public string? StaffReason { get; init; }

    /// <summary>
    /// Gets or inits the ID of the parent audit event.
    /// </summary>
    public Guid? ParentIdentifier { get; init; }

    /// <summary>
    /// Gets or inits a boolean indicating if the event is a top level event.
    /// </summary>
    public bool IsTopLevel { get; init; } = true;
}

public class AuditEvent : AuditEventBase
{
    /// <summary>
    /// Gets or sets the type of this audit event.
    /// </summary>
    public required AuditEventType Type { get; init; }

    /// <summary>
    /// Gets or inits a boolean indicating if a staff user performed this action.
    /// </summary>
    public bool StaffPerformed { get; init; }

    /// <summary>
    /// Gets or inits the reason given by a staff user for performing this action.
    /// </summary>
    public string? StaffReason { get; init; }
}

public readonly record struct AuditEventType
{
    public const string AIEnhancementSubject = $"{nameof(Organization)}.AIEnhancement";
    public const string RunnerEndpointsSubject = $"{nameof(Organization)}.RunnerEndpoints";

    public string Subject { get; }
    public string Event { get; }

    public AuditEventType(string subject, AuditOperation operation)
        : this(subject, operation.ToString())
    {
    }

    public AuditEventType(string subject, string @event)
    {
        void ValidateElement(string value, string paramName)
        {
            if (value.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '.'))
            {
                throw new ArgumentException(
                    "Audit Event Type is invalid. Components should only contain Alphanumeric characters, '_' or '.'.",
                    paramName);
            }
        }

        // Validate subject and event
        // If this is slow, we can do it only on DEBUG builds.
        // But I'd rather try to be safe now and see how it behaves.
        ValidateElement(subject, nameof(subject));
        ValidateElement(@event, nameof(@event));

        Subject = subject;
        Event = @event;
    }

    public override string ToString() => Subject is null && Event is null ? string.Empty : $"{Subject}:{Event}";

    public static bool TryParse(string val, out AuditEventType type)
    {
        var parts = val.Split(':');
        if (parts.Length != 2)
        {
            type = default;
            return false;
        }

        type = new AuditEventType(parts[0], parts[1]);
        return true;
    }

    public static AuditEventType Parse(string val)
    {
        if (!TryParse(val, out var type))
        {
            throw new FormatException($"Invalid audit event type '{val}'.");
        }

        return type;
    }
}

public static class AuditEventBaseExtensions
{
    /// <summary>
    /// Returns <c>true</c> if we should show the properties of the given <see cref="AuditEventBase"/> to non-staff.
    /// </summary>
    /// <remarks>
    /// Starting off with a hard-coded list. In practice, we may want to render Properties for specific events in a
    /// more human readable manner. But this will do in a pinch for now.
    /// </remarks>
    /// <param name="auditEventBase">The Audit Event.</param>
    public static bool ShouldShowProperties(this AuditEventBase auditEventBase)
        => auditEventBase is AuditEvent { Type.Subject: AuditEventType.AIEnhancementSubject };
}

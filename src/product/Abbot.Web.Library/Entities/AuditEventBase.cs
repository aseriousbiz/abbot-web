using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Serious.Abbot.Entities;

[Table("AuditEvents")]
public abstract class AuditEventBase : OrganizationEntityBase<AuditEventBase>
{
    // TAKE CARE when changing these. Existing properties may have been serialized to the database with previous values.
    static readonly JsonSerializerSettings PropertiesSerializerSettings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Converters = new List<JsonConverter>
        {
            // This changes the default _serialization_ to serialize enums as strings.
            // Deserializing still allows numbers, so this is safe to do even if we serialized some older events with numeric enum values.
            new StringEnumConverter()
        }
    };

    public string Discriminator { get; set; } = null!;

    /// <summary>
    /// Identifier used to uniquely identify an event that's safe to present to users.
    /// </summary>
    public Guid Identifier { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The identifier for the parent <see cref="AuditEventBase"/>.
    /// </summary>
    public Guid? ParentIdentifier { get; set; }

    /// <summary>
    /// The current activity id.
    /// </summary>
    /// <remarks>
    /// We use <see cref="TraceContext.CurrentActivityId"/> to get this.
    /// </remarks>
    public string? TraceId { get; set; }

    /// <summary>
    /// The <see cref="Member"/> that initiated the logged action.
    /// This member may not come from the same organization as the event itself (such as when staff perform operations).
    /// </summary>
    public Member? ActorMember { get; set; }

    /// <summary>
    /// The Id of the <see cref="Member"/> that initiated the logged action.
    /// This member may not come from the same organization as the event itself (such as when staff perform operations).
    /// </summary>
    public int? ActorMemberId { get; set; }

    /// <summary>
    /// The <see cref="User"/> that initiated the logged action.
    /// </summary>
    public User Actor { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="User"/> that initiated the logged action.
    /// </summary>
    public int ActorId { get; set; }

    /// <summary>
    /// A sanitized description of the logged action.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// A detailed description of the logged action.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// If set to true, this event is visible to staff only.
    /// </summary>
    public bool StaffOnly { get; set; }

    /// <summary>
    /// If an error is being logged, this stores the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or inits the ID of the entity this audit event relates to.
    /// The entity type for this is determined by the subclass.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating if this is a "top-level" audit event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A top-level audit event is one displayed on the main activity log.
    /// Non-top-level audit events MUST have <see cref="ParentIdentifier"/> set,
    /// and will only be shown when viewing the details of the parent event.
    /// </para>
    /// <para>
    /// Be aware that the inverse is not entirely true.
    /// A top-level audit event may have a <see cref="ParentIdentifier"/> set.
    /// If it does, it will be shown BOTH in the top-level list AND when viewing the details of the parent event.
    /// </para>
    /// </remarks>
    public bool IsTopLevel { get; set; } = true;

    [Column("Properties", TypeName = "jsonb")]
    public string? SerializedProperties { get; set; }

    [NotMapped]
    public virtual bool HasDetails => false;

    static readonly object UninitializedSentinel = new();
    object? _cachedProperties = UninitializedSentinel;
    public T? ReadProperties<T>() where T : class
    {
        if (ReferenceEquals(_cachedProperties, UninitializedSentinel))
        {
            // _cachedProperties is not initialized, so we need to initialize it.
            // It _might_ be being initialized by another thread, but we'll cover that case later.

            object? newValue = null;
            if (SerializedProperties is not null)
            {
                newValue = JsonConvert.DeserializeObject<T>(SerializedProperties);
            }

            // Only set _cachedProperties if it's currently uninitialized.
            // We don't expect an AuditEventBase to be accessed by multiple threads, but
            // if it is, we don't want to write to _cachedProperties multiple times.
            Interlocked.CompareExchange(ref _cachedProperties, newValue, UninitializedSentinel);
        }

        return (T?)_cachedProperties;
    }

#pragma warning disable CA1044
    public object Properties
#pragma warning restore CA1044
    {
        set => SerializedProperties = JsonConvert.SerializeObject(value, PropertiesSerializerSettings);
    }
}

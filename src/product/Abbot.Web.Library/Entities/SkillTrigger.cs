using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// Base class for the user skill triggers.
/// </summary>
public abstract class SkillTrigger<TEntity> : SkillTrigger, IEntity<TEntity> where TEntity : class, IEntity<TEntity>
{
#pragma warning disable CA1033
    Id<TEntity> IEntity<TEntity>.GetPrimaryKey() => this;
#pragma warning restore CA1033

    public static implicit operator Id<TEntity>(SkillTrigger<TEntity> entity) => new(entity.Id);
}

/// <summary>
/// Base class for the user skill triggers.
/// </summary>
public abstract class SkillTrigger : TrackedEntityBase, INamedEntity, ISkillChildEntity, IAuditableEntity
{
    /// <summary>
    /// Friendly name for this skill conversation reference. Typically the room or channel
    /// that messages are sent to.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The platform specific Id of the room where the trigger is attached.
    /// </summary>
    public string RoomId { get; set; } = null!;

    /// <summary>
    /// A description of the skill.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The <see cref="Skill"/> this trigger belongs to.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Skill"/> this trigger belongs to.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// Discriminator column.
    /// </summary>
    public string TriggerType { get; set; } = null!;

    protected TAuditEvent CreateAuditEventInstance<TAuditEvent>()
        where TAuditEvent : TriggerChangeEvent, new()
    {
        var auditEvent = new TAuditEvent
        {
            EntityId = Id,
            TriggerDescription = Description,
            Room = Name
        };

        return auditEvent;
    }

    public abstract AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation);
}

namespace Serious.Abbot.Entities;

public abstract class TrackedEntityBase<TEntity> : TrackedEntityBase, IEntity<TEntity>
    where TEntity : class, IEntity<TEntity>
{
#pragma warning disable CA1033
    Id<TEntity> IEntity<TEntity>.GetPrimaryKey() => this;
#pragma warning restore CA1033

    public static implicit operator Id<TEntity>(TrackedEntityBase<TEntity> entity) => new(entity.Id);

    public static implicit operator Id<TEntity>?(TrackedEntityBase<TEntity>? entity) =>
         (Id<TEntity>?)entity?.Id;
}

/// <summary>
/// Base class for an entity that stores the <see cref="Member"/> that
/// created and modified it.
/// </summary>
public abstract class TrackedEntityBase : EntityBase, ITrackedEntity
{
    /// <summary>
    /// The <see cref="User"/> that created this entity.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="User"/> that created this entity.
    /// </summary>
    public int CreatorId { get; set; }

    /// <summary>
    /// The date (UTC) when this entity was last modified.
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// The <see cref="Member"/> that last modified this entity.
    /// </summary>
    public User ModifiedBy { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="User"/> that last modified this entity.
    /// </summary>
    public int ModifiedById { get; set; }
}

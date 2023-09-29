namespace Serious.Abbot.Entities;

/// <summary>
/// Base class for all entities that are stored in the database.
/// </summary>
public abstract class EntityBase : IEntity
{
    /// <summary>
    /// The primary key identifier for this entity.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The date (UTC) when this entity was created.
    /// </summary>
    public DateTime Created { get; set; }
}

public abstract class EntityBase<TEntity> : EntityBase, IEntity<TEntity>
    where TEntity : class, IEntity<TEntity>
{
#pragma warning disable CA1033
    Id<TEntity> IEntity<TEntity>.GetPrimaryKey() => this;
#pragma warning restore CA1033

    public static implicit operator Id<TEntity>(EntityBase<TEntity> entity) => new(entity.Id);

    public static implicit operator Id<TEntity>?(EntityBase<TEntity>? entity) =>
         (Id<TEntity>?)entity?.Id;
}

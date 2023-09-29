using System;

namespace Serious.Abbot.Entities;

/// <summary>
/// An entity that's stored in the database.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The primary key identifier for this entity.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The date (UTC) when this entity was created.
    /// </summary>
    // This is a DateTime because there's no Postgres type that stores date with offset.
    public DateTime Created { get; set; }
}

/// <summary>
/// Entity with a strongly typed way to access the primary key.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntity<TEntity> : IEntity where TEntity : class, IEntity
{
    /// <summary>
    /// Returns the primary key identifier for this entity.
    /// </summary>
    Id<TEntity> GetPrimaryKey();
}

using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Base abstraction for storing and retrieving entities from an underlying data store. In our case,
/// that store is a database.
/// </summary>
/// <typeparam name="TEntity">The type of entity that derives from <see cref="ITrackedEntity"/></typeparam>
public interface IRepository<TEntity> where TEntity : class, ITrackedEntity
{
    /// <summary>
    /// Create an entity.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="creator">The user that created the entity.</param>
    Task<TEntity> CreateAsync(TEntity entity, User creator);

    /// <summary>
    /// Deletes the entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="actor">The user that deleted the entity.</param>
    Task RemoveAsync(TEntity entity, User actor);

    /// <summary>
    /// Updates the entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="actor">The user that updated the entity.</param>
    Task UpdateAsync(TEntity entity, User actor);
}

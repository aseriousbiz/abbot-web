using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Base abstraction for repositories that store entities that are scoped by an <see cref="Organization"/> and
/// have a name.
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IOrganizationScopedNamedEntityRepository<TEntity>
    : IOrganizationScopedRepository<TEntity>
    where TEntity : class, INamedEntity, ITrackedEntity, IOrganizationEntity
{
    /// <summary>
    /// Retrieve an entity by name and organization.
    /// </summary>
    /// <param name="name">The name of the entity.</param>
    /// <param name="organization">The organization the entity belongs to.</param>
    Task<TEntity?> GetAsync(string name, Organization organization);
}

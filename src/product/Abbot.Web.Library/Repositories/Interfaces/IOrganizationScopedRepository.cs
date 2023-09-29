using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Base class for repositories that work with organization scoped entities (aka <see cref="IOrganizationEntity"/>).
/// These are entities that belong to an organization and have an <see cref="Organization"/> property as well as
/// a OrganizationId. Pretty much every entity in Abbot is one of these.
/// </summary>
public interface IOrganizationScopedRepository<TEntity>
    : IRepository<TEntity> where TEntity : class, ITrackedEntity, IOrganizationEntity
{
    /// <summary>
    /// Retrieve all entities of this type for the organization.
    /// </summary>
    /// <param name="organization">The organization the entities belongs to.</param>
    Task<IReadOnlyList<TEntity>> GetAllAsync(Organization organization);

    /// <summary>
    /// Retrieves a page of all entities for the organization.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A <see cref="IPaginatedList{TEntity}"/> containing the specified page of results.</returns>
    Task<IPaginatedList<TEntity>> GetAllAsync(Organization organization, int pageNumber, int pageSize);

    /// <summary>
    /// Get a queryable of all entities fro this organization. This allows callers to apply additional
    /// includes or filtering.
    /// </summary>
    /// <param name="organization">The organization the entities belongs to.</param>
    IQueryable<TEntity> GetQueryable(Organization organization);

    /// <summary>
    /// Retrieves an entity by its id and organization.
    /// </summary>
    /// <param name="id">The Id of the entity.</param>
    /// <param name="organization">The organization the entity belongs to.</param>
    /// <returns></returns>
    Task<TEntity?> GetByIdAsync(int id, Organization organization);
}

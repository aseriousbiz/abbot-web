using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Base class for repositories that work with organization scoped entities (aka <see cref="IOrganizationEntity"/>).
/// These are entities that belong to an organization and have an <see cref="Organization"/> property as well as
/// a OrganizationId. Pretty much every entity in Abbot is one of these.
/// </summary>
public abstract class OrganizationScopedRepository<TEntity>
    : Repository<TEntity>, IOrganizationScopedRepository<TEntity>
    where TEntity : class, ITrackedEntity, IOrganizationEntity
{
    protected IAuditLog AuditLog { get; }

    protected OrganizationScopedRepository(AbbotContext db, IAuditLog auditLog) : base(db)
    {
        AuditLog = auditLog;
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(Organization organization)
    {
        return await GetQueryable(organization).ToListAsync();
    }

    public async Task<IPaginatedList<TEntity>> GetAllAsync(Organization organization, int pageNumber, int pageSize)
    {
        var query = GetQueryable(organization);
        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }

    public virtual IQueryable<TEntity> GetQueryable(Organization organization)
    {
        return GetEntitiesQueryable()
            .Include(e => e.Organization)
            .Include(e => e.Creator)
            .Include(e => e.ModifiedBy)
            .Where(s => s.OrganizationId == organization.Id);
    }

    protected virtual IQueryable<TEntity> GetEntitiesQueryable()
    {
        return Entities;
    }

    public async Task<TEntity?> GetByIdAsync(int id, Organization organization)
    {
        return await GetQueryable(organization)
            .SingleEntityOrDefaultAsync(e => e.Id == id);
    }

    protected virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(
        Organization organization,
        Expression<Func<TEntity, bool>>? predicate)
    {
        var query = GetQueryable(organization);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// Retrieves a set of entity instances by IDs.
    /// </summary>
    /// <param name="ids">The database IDs for entities.</param>
    /// <param name="organization">The organization containing the entity.</param>
    /// <returns>A list of <see cref="EntityResult{TEntity}"/> for each passed in entity Id.</returns>
    public async Task<IReadOnlyList<EntityLookupResult<TEntity, Id<TEntity>>>> GetAllByIdsAsync(
        IEnumerable<Id<TEntity>> ids,
        Organization organization)
    {
        var idList = ids.ToList();
        var intIds = idList.Select(id => id.Value).ToList();
        var entities = await GetQueryable(organization)
            .Where(t => intIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        EntityLookupResult<TEntity, Id<TEntity>> GatherResult(Id<TEntity> id)
        {
            var exists = entities.TryGetValue(id, out var entity);
            var resultType = exists
                ? EntityResultType.Success
                : EntityResultType.NotFound;
            return new EntityLookupResult<TEntity, Id<TEntity>>(resultType, id, entity, null);
        }

        return idList.Select(GatherResult).ToList();
    }

    protected override Task LogEntityCreatedAsync(TEntity entity, User creator)
    {
        return entity is IAuditableEntity auditableEntity
            ? AuditLog.LogEntityCreatedAsync(auditableEntity, creator, entity.Organization)
            : Task.CompletedTask;
    }

    protected override Task LogEntityDeletedAsync(TEntity entity, User actor)
    {
        return entity is IAuditableEntity auditableEntity
            ? AuditLog.LogEntityDeletedAsync(auditableEntity, actor, entity.Organization)
            : Task.CompletedTask;
    }

    protected override Task LogEntityChangedAsync(TEntity entity, User actor)
    {
        return entity is IAuditableEntity auditableEntity
            ? AuditLog.LogEntityChangedAsync(auditableEntity, actor, entity.Organization)
            : Task.CompletedTask;
    }
}

using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public abstract class Repository<TEntity> : IRepository<TEntity>
    where TEntity : class, ITrackedEntity
{
    protected Repository(AbbotContext db)
    {
        Db = db;
    }

    protected AbbotContext Db { get; }

    public virtual async Task<TEntity> CreateAsync(TEntity entity, User creator)
    {
        entity.Creator = creator;
        entity.ModifiedBy = creator;

        await Entities.AddAsync(entity);
        await Db.SaveChangesAsync();

        await LogEntityCreatedAsync(entity, creator);
        return entity;
    }

    public virtual async Task RemoveAsync(TEntity entity, User actor)
    {
        await LogEntityDeletedAsync(entity, actor);

        if (entity is IRecoverableEntity)
        {
            // It'll be a soft delete, so update ModifiedBy
            entity.ModifiedBy = actor;
        }

        Entities.Remove(entity);
        await Db.SaveChangesAsync();
    }

    public async Task UpdateAsync(TEntity entity, User actor)
    {
        entity.ModifiedBy = actor;
        await Db.SaveChangesAsync();
        await LogEntityChangedAsync(entity, actor);
    }

    protected abstract DbSet<TEntity> Entities { get; }

    protected abstract Task LogEntityCreatedAsync(TEntity entity, User creator);
    protected abstract Task LogEntityDeletedAsync(TEntity entity, User actor);
    protected abstract Task LogEntityChangedAsync(TEntity entity, User actor);
}

public abstract class NonAuditingRepository<TEntity> : Repository<TEntity>
    where TEntity : class, ITrackedEntity
{
    protected override Task LogEntityCreatedAsync(TEntity entity, User creator)
    {
        return Task.CompletedTask;
    }
    protected override Task LogEntityDeletedAsync(TEntity entity, User actor)
    {
        return Task.CompletedTask;
    }
    protected override Task LogEntityChangedAsync(TEntity entity, User actor)
    {
        return Task.CompletedTask;
    }

    protected NonAuditingRepository(AbbotContext db) : base(db)
    {
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

/// <summary>
/// <para>
/// Manages settings. Some settings are App Settings that should only be used by Staff users.
/// Use <see cref="SetWithAuditingAsync"/> and <see cref="RemoveWithAuditingAsync"/> for those settings to ensure
/// changes are recorded in the Activity Log.
/// </para>
/// <para>
/// Other settings are internal system settings that don't need to be logged to the Activity Log.
/// </para>
/// </summary>
public class SettingsManager : ISettingsManager
{
    readonly AbbotContext _db;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;
    readonly NonAuditingSettingNonAuditingRepository _nonAuditingRepository;

    /// <summary>
    /// Constructs a <see cref="SettingsManager"/>
    /// </summary>
    /// <param name="db">The <see cref="AbbotContext"/>.</param>
    /// <param name="auditLog">The <see cref="IAuditLog"/>.</param>
    /// <param name="clock">The clock abstraction.</param>
    public SettingsManager(AbbotContext db, IAuditLog auditLog, IClock clock)
    {
        _db = db;
        _nonAuditingRepository = new NonAuditingSettingNonAuditingRepository(db);
        _auditLog = auditLog;
        _clock = clock;
    }

    SettingRepository GetRepository(Organization organization)
    {
        // Even though settings are for the whole site, we still need an organization to host the
        // audit log entries. Since we always log in with _our_ Slack organization to access staff tools,
        // we'll use that organization for this.
        return new SettingRepository(_db, _auditLog, organization);
    }

    /// <inheritdoc/>
    public async Task<Setting> SetAsync(SettingsScope scope, string name, string value, User actor)
    {
        return await SetAsync(scope, name, value, actor, null, _nonAuditingRepository);
    }

    public async Task<Setting> SetAsync(SettingsScope scope, string name, string value, User actor, TimeSpan ttl)
    {
        return await SetAsync(scope, name, value, actor, ttl, _nonAuditingRepository);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(SettingsScope scope, string name, User actor)
    {
        await RemoveAsync(scope, name, actor, _nonAuditingRepository);
    }

    /// <inheritdoc/>
    public async Task<Setting> SetWithAuditingAsync(SettingsScope scope, string name, string value, User actor,
        Organization organization)
    {
        var repository = GetRepository(organization);
        return await SetAsync(scope, name, value, actor, null, repository);
    }

    async Task<Setting> SetAsync(
        SettingsScope scope,
        string name,
        string value,
        User actor,
        TimeSpan? ttl,
        IRepository<Setting> repository)
    {
        var expiry = _clock.UtcNow + ttl;

        var setting = await GetAsync(scope, name);
        if (setting is not null)
        {
            setting.Expiry = expiry;
            setting.Value = value;
            await repository.UpdateAsync(setting, actor);
            return setting;
        }

        return await repository.CreateAsync(new Setting
        {
            Name = name,
            Value = value,
            Expiry = expiry,
            Scope = scope.Name,
        }, actor);
    }

    /// <inheritdoc/>
    public async Task RemoveWithAuditingAsync(SettingsScope scope, string name, User actor, Organization organization)
    {
        var repository = GetRepository(organization);

        await RemoveAsync(scope, name, actor, repository);
    }

    async Task RemoveAsync(SettingsScope scope, string name, User actor, IRepository<Setting> repository)
    {
        var setting = await GetAsync(scope, name);
        if (setting is not null)
        {
            await repository.RemoveAsync(setting, actor);
        }
    }

    /// <inheritdoc/>
    public async Task<Setting?> GetAsync(SettingsScope scope, string name)
    {
        var setting = await Queryable
            .SingleOrDefaultAsync(s => s.Scope == scope.Name && s.Name == name);

        if (setting?.Expiry <= _clock.UtcNow)
        {
            // Don't purge the setting, leave that for the background job.
            return null;
        }

        return setting;
    }

    /// <inheritdoc/>
    public async Task<Setting?> GetCascadingAsync(string name, params SettingsScope[] scopes)
    {
        // Just fetch all matching settings
        var names = scopes.Select(s => s.Name).ToArray();
        var settings = await Queryable.Where(s => s.Name == name && names.Any(scope => scope == s.Scope)).ToListAsync();

        // Now find the first match.
        foreach (var scope in scopes)
        {
            var setting = settings.SingleOrDefault(s => s.Scope == scope.Name);

            if (setting is not null && (setting.Expiry is null || setting.Expiry > _clock.UtcNow))
            {
                return setting;
            }
        }

        // No matches
        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Setting>> GetAllAsync(SettingsScope scope)
    {
        return await Queryable
            .Where(s => s.Scope == scope.Name)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Setting>> GetAllAsync(SettingsScope scope, string prefix)
    {
        return await Queryable
            .Where(s => s.Scope == scope.Name && s.Name.StartsWith(prefix))
            .ToListAsync();
    }

    public async Task<int> RemoveExpiredSettingsAsync(DateTime asOfUtc, CancellationToken cancellationToken)
    {
        // We doin' raw SQL here. We really don't want to load all the settings into memory.
        return await _db.Database.ExecuteSqlRawAsync(
            sql: "DELETE FROM \"Settings\" WHERE \"Expiry\" IS NOT NULL AND \"Expiry\" < {0}",
            parameters: new object[] { asOfUtc },
            cancellationToken: cancellationToken);
    }

    IQueryable<Setting> Queryable => _db.Settings
        .Include(s => s.Creator)
        .Include(s => s.ModifiedBy);

    class SettingRepository : Repository<Setting>
    {
        readonly IAuditLog _auditLog;
        readonly Organization _organization;

        public SettingRepository(AbbotContext db, IAuditLog auditLog, Organization organization) : base(db)
        {
            _auditLog = auditLog;
            _organization = organization;
            Entities = db.Settings;
        }

        protected override DbSet<Setting> Entities { get; }

        protected override async Task LogEntityCreatedAsync(Setting entity, User creator)
        {
            await _auditLog.LogEntityCreatedAsync(entity, creator, _organization);
        }

        protected override async Task LogEntityDeletedAsync(Setting entity, User actor)
        {
            await _auditLog.LogEntityDeletedAsync(entity, actor, _organization);
        }

        protected override async Task LogEntityChangedAsync(Setting entity, User actor)
        {
            await _auditLog.LogEntityChangedAsync(entity, actor, _organization);
        }
    }

    class NonAuditingSettingNonAuditingRepository : NonAuditingRepository<Setting>
    {
        public NonAuditingSettingNonAuditingRepository(AbbotContext db) : base(db)
        {
            Entities = db.Settings;
        }

        protected override DbSet<Setting> Entities { get; }
    }
}

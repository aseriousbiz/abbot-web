using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

public class PackageRepository : OrganizationScopedRepository<Package>, IPackageRepository
{
    readonly IAuditLog _auditLog;
    public PackageRepository(AbbotContext db, IAuditLog auditLog) : base(db, auditLog)
    {
        _auditLog = auditLog;
    }

    protected override DbSet<Package> Entities => Db.Packages;

    public async ValueTask<Package?> GetAsync(Skill skill)
    {
        return await IncludeQueryable(Entities)
            .SingleOrDefaultAsync(s => s.SkillId == skill.Id);
    }

    public async ValueTask<Package?> GetAsync(int id)
    {
        return await IncludeQueryable(Entities)
            .SingleOrDefaultAsync(p => p.Id == id);
    }

    public async ValueTask<Package?> GetDetailsAsync(string organizationSlug, string name)
    {
        return await IncludeQueryable(Entities)
            .ThenInclude(sv => sv.InstalledSkills)
            .Include(sv => sv.Versions)
            .ThenInclude(sv => sv.Creator)
            .Where(p => (p.Organization.Domain == $"{organizationSlug}.slack.com"
                         || p.Organization.PlatformId == organizationSlug)
                        && p.Skill.Name == name)
            .SingleOrDefaultAsync();
    }

    public async ValueTask<Package> CreateAsync(PackageCreateModel createModel, Skill skill, User user)
    {
        var package = createModel.CreatePackageInstance(skill, user);
        await Entities.AddAsync(package);
        await Db.SaveChangesAsync();
        await _auditLog.LogPackagePublishedAsync(package, user, skill.Organization);
        return package;
    }

    public async ValueTask<PackageVersion> PublishNewVersionAsync(
        PackageUpdateModel updateModel,
        Package package,
        Skill skill,
        User user)
    {
        updateModel.Apply(package, skill, user);
        var currentVersion = package.GetLatestVersion();
        if (!Enum.TryParse<ChangeType>(updateModel.ChangeType, out var changeType))
        {
            throw new InvalidOperationException("Attempted to publish a new version without specifying a change type.");
        }
        var newVersion = currentVersion.CreateNextVersion(changeType);
        newVersion.Creator = user;
        newVersion.ReleaseNotes = updateModel.ReleaseNotes ?? string.Empty;
        newVersion.CodeCacheKey = skill.CacheKey;
        package.Versions.Add(newVersion);
        await Db.SaveChangesAsync();
        await _auditLog.LogPackagePublishedAsync(package, user, skill.Organization);
        return newVersion;
    }

    public async Task UpdatePackageMetadataAsync(Package package, string readme, bool listed, User user)
    {
        if (package.Readme.Equals(readme, StringComparison.Ordinal) && package.Listed == listed)
        {
            return;
        }

        bool packageUnlisted = package.Listed && !listed;
        bool readmeChanged = !package.Readme.Equals(readme, StringComparison.Ordinal);

        package.Readme = readme;
        package.Listed = listed;
        package.Modified = DateTime.UtcNow;
        package.ModifiedBy = user;
        await Db.SaveChangesAsync();

        if (packageUnlisted && !listed)
        {
            await _auditLog.LogPackageUnlistedAsync(package, user, package.Organization);
        }

        if (readmeChanged)
        {
            await _auditLog.LogPackageChangedAsync(package, user, package.Organization);
        }
    }

    public IQueryable<Package> GetQueryable()
    {
        return IncludeQueryable(Entities)
            .ThenInclude(sv => sv.InstalledSkills)
            .Include(sv => sv.Versions);
    }

    public override IQueryable<Package> GetQueryable(Organization organization)
    {
        return IncludeQueryable(base.GetQueryable(organization));
    }

    static IIncludableQueryable<Package, List<PackageVersion>> IncludeQueryable(IQueryable<Package> queryable)
    {
        return queryable
            .Include(p => p.Organization)
            .Include(p => p.Skill)
            .Include(p => p.Versions);
    }
}

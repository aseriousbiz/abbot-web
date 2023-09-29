using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository used to manage skills in the database.
/// </summary>
public class SkillRepository : OrganizationScopedRepository<Skill>, ISkillRepository
{
    readonly ISkillAuditLog _auditLog;

    public SkillRepository(AbbotContext db, ISkillAuditLog skillAuditLog, IAuditLog auditLog) : base(db, auditLog)
    {
        _auditLog = skillAuditLog;
        Entities = db.Skills;
    }

    public override Task<Skill> CreateAsync(Skill entity, User creator)
    {
        entity.CacheKey = SkillCompiler.ComputeCacheKey(entity.Code);
        return base.CreateAsync(entity, creator);
    }

    public async Task<Skill?> GetAsync(
        string name,
        Organization organization)
    {
        name = name.ToLowerInvariant();
        return await GetSkillListQueryable(organization)
            .Include(s => s.Patterns)
            .Include(s => s.SignalSubscriptions)
            .ThenInclude(s => s.Creator)
            .Include(s => s.SourcePackageVersion)
            .ThenInclude(pv => pv!.Package)
            .ThenInclude(p => p.Versions)
            .ThenInclude(v => v.Creator)
            .Include(s => s.Exemplars)
            .SingleOrDefaultAsync(s => s.Name == name);
    }

    public async Task<Skill?> GetByIdAsync(Id<Skill> id)
    {
        return await Entities.Include(s => s.Organization).SingleEntityOrDefaultAsync(id);
    }

    public IQueryable<Skill> GetSkillListQueryable(Organization organization)
    {
        return GetQueryable(organization)
            .Include(s => s.Package)
            .ThenInclude(p => p!.Versions)
            .Include(s => s.Triggers)
            .ThenInclude(t => t.Creator)
            .Include(s => s.Secrets)
            .Include(s => s.SourcePackageVersion)
            .ThenInclude(pv => pv!.Package)
            .ThenInclude(pv => pv.Organization)
            .Include(s => s.SourcePackageVersion)
            .ThenInclude(pv => pv!.Package)
            .ThenInclude(p => p.Skill)
            .ThenInclude(p => p.Package!.Creator);
    }

    public async Task<IReadOnlyList<Skill>> SearchAsync(string? query, string? currentValue, int limit, Organization organization)
    {
        // Let's query the current value.
        var currentSkillList = (currentValue is { Length: > 0 }
            ? await GetAsync(currentValue, organization) is { } currentSkill
                ? new List<Skill> { currentSkill }
                : null
            : null) ?? new List<Skill>();

        if (query is not { Length: > 0 })
        {
            return currentSkillList;
        }

        // Limit the number of results to a reasonable number.
        limit = Math.Max(20, Math.Min(3, limit));

        // Concat the lists into a single list.

        int Score(string name) => name switch
        {
            _ when name == query => 3,
            _ when name.StartsWith(query, StringComparison.OrdinalIgnoreCase) => 2,
            _ when name.Contains(query, StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0,
        };

        var matchingSkills = await GetSkillListQueryable(organization)
            .Where(s => s.Name.ToLower().Contains(query.ToLower()))
            .ToListAsync();

        // Put the current values first.
        var resultingSkills = currentSkillList.Union(
            // Then the skills that match the query.
            matchingSkills.Where(s =>
                // But filter out current values so they don't show up twice.
                !currentSkillList.Any(cs => cs.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase))));

        return resultingSkills
            .Select(s => new { Skill = s, Score = Score(s.Name) })
            .Where(e => e.Score > 0)
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Skill.Name)
            .Select(e => e.Skill)
            .Take(limit)
            .ToList();
    }

    public async Task<bool> UpdateAsync(Skill original, SkillUpdateModel updateModel, User modifiedBy)
    {
        // Null out properties that are not changed.
        if (original.Name.Equals(updateModel.Name, StringComparison.Ordinal))
        {
            updateModel.Name = null;
        }
        if (original.UsageText.Equals(updateModel.UsageText, StringComparison.Ordinal))
        {
            updateModel.UsageText = null;
        }
        if (original.Code.Equals(updateModel.Code, StringComparison.Ordinal))
        {
            updateModel.Code = null;
        }
        if (original.Restricted.Equals(updateModel.Restricted))
        {
            updateModel.Restricted = null;
        }
        if (original.Enabled.Equals(updateModel.Enabled))
        {
            updateModel.Enabled = null;
        }
        if (updateModel.OnlyChangedEnabled)
        {
            await ToggleEnabledAsync(original, updateModel.Enabled.GetValueOrDefault(), modifiedBy);
            return true;
        }

        // This saves a snapshot of the current version to the SkillVersions table. The current version is the
        // skill itself.
        var skillVersion = updateModel.ToVersionSnapshot(original);
        await Db.SkillVersions.AddAsync(skillVersion);

        updateModel.ApplyChanges(original);
        if (!Db.Entry(original).Properties.Any(p => p.IsModified))
        {
            // No changes to save.
            return false;
        }

        original.Modified = DateTime.UtcNow;
        original.ModifiedBy = modifiedBy;
        skillVersion.Created = original.Modified;
        await base.UpdateAsync(original, modifiedBy);

        if (updateModel.Enabled is not null)
        {
            // We want a special log entry for enabling/disabling skills.
            await _auditLog.LogSkillEnabledChangedAsync(original, modifiedBy);
        }

        await _auditLog.LogSkillChangedAsync(original, skillVersion, modifiedBy);
        return true;
    }

    public async Task ToggleEnabledAsync(Skill skill, bool enabled, User actor)
    {
        if (skill.Enabled != enabled)
        {
            skill.Enabled = enabled;
            await SaveChangesAsync();
            await _auditLog.LogSkillEnabledChangedAsync(skill, actor);
        }
    }

    public async Task<Skill?> GetWithVersionsAsync(string name, Organization organization)
    {
        return await GetQueryable(organization)
            .Include(s => s.Versions)
            .ThenInclude(v => v.Creator)
            .SingleOrDefaultAsync(s => s.Name == name && s.OrganizationId == organization.Id);
    }

    public async Task<Skill?> GetWithDataAsync(Id<Skill> skillId)
    {
        return await Entities
            .AsNoTrackingWithIdentityResolution()
            .Include(s => s.Data.Where(d => d.Scope == d.Skill.Scope))
            .SingleOrDefaultAsync(s => s.Id == skillId.Value
                && s.Scope == SkillDataScope.Organization // TODO: to support querying data from non-Organization scoped skills, we need the execution context (user/conversation/room id)
            );
    }

    public async Task<Skill?> GetWithDataAsync(Id<Skill> skillId, SkillDataScope scope, string? contextId)
    {
        return await Entities
            .AsNoTrackingWithIdentityResolution()
            .Include(s => s.Data.Where(d => d.Scope == scope && d.ContextId == contextId.ToNullIfEmpty()))
            .SingleEntityOrDefaultAsync(skillId);
    }

    public async Task<SkillData?> GetDataAsync(Id<Skill> skillId, string key)
    {
        key = key.ToUpperInvariant();
        return await Db.SkillData
            // Since we don't allow calling a deleted skill, this should be safe.
            // If a skill was "deleted" in the midst of a call, we should allow it to finish.
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(d => d.SkillId == skillId.Value && d.Key == key
                && d.Scope == SkillDataScope.Organization // TODO: to support querying data from non-Organization scoped skills, we need the execution context (user/conversation/room id)
            );
    }

    public async Task<SkillData?> GetDataAsync(Id<Skill> skillId, string key, SkillDataScope scope, string? contextId)
    {
        key = key.ToUpperInvariant();
        return await Db.SkillData
            // Since we don't allow calling a deleted skill, this should be safe.
            // If a skill was "deleted" in the midst of a call, we should allow it to finish.
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.SkillId == skillId.Value && d.Key == key && d.Scope == scope && d.ContextId == contextId.ToNullIfEmpty());
    }

    public async Task AddDataAsync(SkillData data)
    {
        data.Key = data.Key.ToUpperInvariant();
        await Db.SkillData.AddAsync(data);
        await Db.SaveChangesAsync();
    }

    public Task DeleteDataAsync(SkillData data)
    {
        Db.SkillData.Remove(data);
        return Db.SaveChangesAsync();
    }

    public virtual Task SaveChangesAsync()
    {
        return Db.SaveChangesAsync();
    }

    public async Task<EntityResult<SkillExemplar>> AddExemplarAsync(Skill skill, string text, ExemplarProperties properties, Member actor)
    {
        var exemplar = new SkillExemplar()
        {
            Skill = skill,
            SkillId = skill.Id,
            Organization = skill.Organization,
            OrganizationId = skill.OrganizationId,
            Created = DateTime.UtcNow,
            Exemplar = text,
            Properties = properties,
        };
        await Db.SkillExemplars.AddAsync(exemplar);
        await Db.SaveChangesAsync();

        await AuditLog.LogAuditEventAsync(new()
        {
            Type = new("Skill.Exemplar", AuditOperation.Created),
            Actor = actor,
            Organization = skill.Organization,
            Description = $"Created an exemplar for the `{skill.Name}` skill.",
            EntityId = exemplar.Id,
            Properties = new {
                Text = text,
                properties.Arguments,
            }
        });

        return EntityResult.Success(exemplar);
    }

    public async Task<EntityResult<SkillExemplar>> UpdateExemplarAsync(SkillExemplar exemplar, string text, ExemplarProperties properties, Member actor)
    {
        exemplar.Exemplar = text;
        exemplar.Properties = properties;
        Db.SkillExemplars.Update(exemplar);
        await Db.SaveChangesAsync();

        await AuditLog.LogAuditEventAsync(new()
        {
            Type = new("Skill.Exemplar", AuditOperation.Changed),
            Actor = actor,
            Organization = exemplar.Skill.Organization,
            Description = $"Updated an exemplar for the `{exemplar.Skill.Name}` skill.",
            EntityId = exemplar.Id,
            Properties = new {
                Text = text,
                properties.Arguments,
            }
        });
        return EntityResult.Success(exemplar);
    }

    public async Task<EntityResult> RemoveExemplarAsync(SkillExemplar exemplar, Member actor)
    {
        Db.SkillExemplars.Remove(exemplar);
        await Db.SaveChangesAsync();

        await AuditLog.LogAuditEventAsync(new()
        {
            Type = new("Skill.Exemplar", AuditOperation.Removed),
            Actor = actor,
            Organization = exemplar.Organization,
            Description = $"Removed an exemplar for the `{exemplar.Skill.Name}` skill.",
            EntityId = exemplar.Id,
            Properties = new {
                Text = exemplar.Exemplar,
                exemplar.Properties.Arguments,
            }
        });

        return EntityResult.Success();
    }

    protected override DbSet<Skill> Entities { get; }

    protected override Task LogEntityChangedAsync(Skill entity, User actor)
    {
        // NOTE: We don't log user skill changes here because that's handled
        // by logging the SkillVersion instead as that represents the changes
        // being made to the skill.
        return Task.CompletedTask;
    }
}

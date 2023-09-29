using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository that manages Http and Scheduled triggers for skills. Used to create, retrieve, update, and delete
/// triggers.
/// </summary>
public class TriggerRepository : ITriggerRepository
{
    static readonly ILogger<TriggerRepository> Log = ApplicationLoggerFactory.CreateLogger<TriggerRepository>();

    readonly AbbotContext _db;
    readonly Repository<SkillHttpTrigger> _httpTriggerRepository;
    readonly Repository<SkillScheduledTrigger> _scheduledTriggerRepository;

    public TriggerRepository(AbbotContext db, IAuditLog auditLog)
    {
        _db = db;
        _httpTriggerRepository = new DelegateTriggerRepository<SkillHttpTrigger>(
            db,
            auditLog,
            db.SkillHttpTriggers);
        _scheduledTriggerRepository = new DelegateTriggerRepository<SkillScheduledTrigger>(db,
            auditLog,
            db.SkillScheduledTriggers);
    }

    /// <summary>
    /// Retrieves the <see cref="SkillHttpTrigger"/> associated with the api token and skill name.
    /// </summary>
    /// <param name="skillName">The name of the skill.</param>
    /// <param name="apiToken">The API Token associated with the skill.</param>
    /// <returns>An instance of <see cref="SkillHttpTrigger"/> with a loaded <see cref="Skill"/> property.</returns>
    public async Task<SkillHttpTrigger?> GetSkillHttpTriggerAsync(string skillName, string apiToken)
    {
        var name = skillName.ToLowerInvariant();
        return await _db.SkillHttpTriggers
            .Include(t => t.Skill)
            .ThenInclude(s => s.Organization)
            .Include(t => t.Creator)
            .ThenInclude(t => t.Members)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Skill.Name == name && t.ApiToken == apiToken);
    }

    public Task<SkillHttpTrigger> CreateHttpTriggerAsync(
        Skill skill,
        string? description,
        IRoom room,
        Member creator)
    {
        var trigger = CreateTriggerInstance<SkillHttpTrigger>(skill, description, room);
        trigger.ApiToken = TokenCreator.CreateStrongAuthenticationToken("abt");
        return _httpTriggerRepository.CreateAsync(trigger, creator.User);
    }

    public Task<SkillScheduledTrigger> CreateScheduledTriggerAsync(
        Skill skill,
        string? arguments,
        string? description,
        string cronSchedule,
        IRoom room,
        Member creator)
    {
        var trigger = CreateTriggerInstance<SkillScheduledTrigger>(skill, description, room);
        trigger.CronSchedule = cronSchedule;
        trigger.Arguments = arguments;
        trigger.TimeZoneId = creator.TimeZoneId ?? "Etc/UTC";
        return _scheduledTriggerRepository.CreateAsync(trigger, creator.User);
    }

    static TTrigger CreateTriggerInstance<TTrigger>(
        Skill skill,
        string? description,
        IRoom room) where TTrigger : SkillTrigger, new()
    {
        // The call site should have ensured the room had a valid name.
        // TriggerSkillBase does that for us.
        Expect.True(room.Name is { Length: > 0 });

        var trigger = new TTrigger
        {
            Name = room.Name,
            RoomId = room.Id,
            Description = description,
            Skill = skill
        };
        skill.Triggers.Add(trigger);

        Log.CreatingTrigger(typeof(TTrigger), room.Id, skill.Id, skill.Name);
        return trigger;
    }

    public async Task<SkillScheduledTrigger?> GetScheduledTriggerAsync(int scheduledTriggerId)
    {
        return await _db.SkillScheduledTriggers
            .Include(t => t.Skill)
            .ThenInclude(s => s.Organization)
            .Include(t => t.Creator)
            .ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == scheduledTriggerId);
    }

    public async Task<TTrigger?> GetSkillTriggerAsync<TTrigger>(
        string skillName,
        string triggerName,
        Organization organization)
        where TTrigger : SkillTrigger
    {
        return await _db.SkillTriggers
            .OfType<TTrigger>()
            .Include(t => t.Skill)
            .ThenInclude(s => s.Organization)
            .Include(t => t.Creator)
            .FirstOrDefaultAsync(t =>
                t.Name == triggerName
                && t.Skill.Name == skillName
                && t.Skill.OrganizationId == organization.Id);
    }

    public Task DeleteTriggerAsync(SkillTrigger trigger, User user)
    {
        return trigger switch
        {
            SkillHttpTrigger httpTrigger => _httpTriggerRepository.RemoveAsync(httpTrigger, user),
            SkillScheduledTrigger scheduledTrigger => _scheduledTriggerRepository.RemoveAsync(scheduledTrigger, user),
            _ => throw new InvalidOperationException($"Unknown trigger type {trigger.GetType().Name}")
        };
    }

    public Task UpdateTriggerDescriptionAsync(SkillTrigger trigger, string? description, User user)
    {
        trigger.Description = description;
        return trigger switch
        {
            SkillHttpTrigger httpTrigger => _httpTriggerRepository.UpdateAsync(httpTrigger, user),
            SkillScheduledTrigger scheduledTrigger => _scheduledTriggerRepository.UpdateAsync(scheduledTrigger, user),
            _ => throw new InvalidOperationException($"Unknown trigger type {trigger.GetType().Name}")
        };
    }

    public async Task<Playbook?> GetPlaybookFromTriggerTokenAsync(string slug, string apiToken)
    {
        if (!PlaybookExtensions.TryGetOrganizationIdFromToken(apiToken, out var organizationId))
        {
            return null;
        }
        var playbooks = await _db.Playbooks
            .Include(p => p.Organization)
            .Where(p => p.Slug == slug && p.OrganizationId == organizationId)
            .ToListAsync();
        return playbooks.SingleOrDefault();
    }

    class DelegateTriggerRepository<TTrigger> : Repository<TTrigger> where TTrigger : SkillTrigger
    {
        readonly IAuditLog _auditLog;

        public DelegateTriggerRepository(AbbotContext db, IAuditLog auditLog, DbSet<TTrigger> entities) : base(db)
        {
            _auditLog = auditLog;
            Entities = entities;
        }

        protected override DbSet<TTrigger> Entities { get; }

        protected override Task LogEntityCreatedAsync(TTrigger entity, User creator)
        {
            return _auditLog.LogEntityCreatedAsync(entity, creator, entity.Skill.Organization);
        }

        protected override Task LogEntityDeletedAsync(TTrigger entity, User actor)
        {
            return _auditLog.LogEntityDeletedAsync(entity, actor, entity.Skill.Organization);
        }

        protected override Task LogEntityChangedAsync(TTrigger entity, User actor)
        {
            return _auditLog.LogEntityChangedAsync(entity, actor, entity.Skill.Organization);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Telemetry;

public class AuditLogReader : IAuditLogReader
{
    readonly AbbotContext _db;

    public AuditLogReader(AbbotContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves an audit entry for the specified Id. IMPORTANT: This does not filter by organization, so check
    /// access rights before showing this.
    /// </summary>
    /// <param name="id">The id of the audit entry.</param>
    public async Task<AuditEventBase?> GetAuditEntryAsync(Guid id)
    {
        return await _db.AuditEvents
            .OfType<AuditEventBase>()
            .Include(e => e.Organization)
            .Include(e => e.Actor)
            .Include(e => e.ActorMember!.User)
            .Where(e => e.Identifier == id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<AuditEventBase>> GetRecentActivityAsync(
        Organization organization,
        int count,
        ActivityTypeFilter activityTypeFilter = ActivityTypeFilter.User)
    {
        var queryable = GetQueryable(organization: organization, eventTypeFilter: activityTypeFilter)
            .Take(count);
        return await queryable.ToListAsync();
    }

    public Task<IPaginatedList<AuditEventBase>> GetAuditEventsAsync(
        Organization organization,
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        ActivityTypeFilter activityTypeFilter,
        DateTime? minDate,
        DateTime? maxDate,
        bool includeStaff)
    {
        var queryable = GetQueryable(statusFilter, activityTypeFilter, minDate, maxDate, organization)
            .Where(e => e.IsTopLevel);
        if (!includeStaff)
        {
            queryable = queryable.Where(e => !e.StaffOnly);
        }

        return PaginatedList.CreateAsync(queryable, pageNumber, pageSize);
    }

    public Task<IPaginatedList<AuditEventBase>> GetAuditEventsForSkillAsync(
        Skill skill,
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        SkillEventFilter skillEventFilter,
        DateTime? minDate,
        DateTime? maxDate)
    {
        var eventTypeFilter = (ActivityTypeFilter)skillEventFilter;
        var queryable = GetQueryable(statusFilter, eventTypeFilter, minDate, maxDate, skill.Organization, skill);

        return PaginatedList.CreateAsync(queryable, pageNumber, pageSize);
    }

    public Task<IPaginatedList<AuditEventBase>> GetAuditEventsForSkillTriggerAsync(
        SkillTrigger trigger,
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        DateTime? minDate,
        DateTime? maxDate)
    {
        var queryable = GetQueryable(
                statusFilter,
                ActivityTypeFilter.All,
                minDate,
                maxDate,
                trigger: trigger);

        return PaginatedList.CreateAsync(queryable, pageNumber, pageSize);
    }

    public Task<IPaginatedList<AuditEventBase>> GetAuditEventsForStaffAsync(
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        ActivityTypeFilter activityTypeFilter,
        DateTime? minDate,
        DateTime? maxDate)
    {
        var queryable = GetQueryable(statusFilter, activityTypeFilter, minDate, maxDate);
        return PaginatedList.CreateAsync(queryable, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<AuditEventBase>> GetRelatedEventsAsync(AuditEventBase parentEvent)
    {
        var query = GetBaseQueryable(parentEvent.Organization);

        // If the parent event is itself a child event, then we include all of it's peers
        if (parentEvent.ParentIdentifier is null)
        {
            query = query.Where(e => e.TraceId == parentEvent.TraceId || e.ParentIdentifier == parentEvent.Identifier);
        }
        else
        {
            query = query.Where(e => e.TraceId == parentEvent.TraceId || e.ParentIdentifier == parentEvent.Identifier || e.ParentIdentifier == parentEvent.ParentIdentifier || e.Identifier == parentEvent.ParentIdentifier);
        }

        return await query.OrderBy(e => e.Id).ToListAsync();
    }

    IQueryable<AuditEventBase> GetBaseQueryable(Organization? organization)
    {
        var queryable = _db.AuditEvents
            .Include(e => e.Actor)
            .Include(e => e.ActorMember!.User)
            .Include(e => e.Organization)
            .OrderByDescending(e => e.Created)
            .ThenByDescending(e => e.Id);

        return organization is null
            ? queryable
            : queryable.Where(e => e.OrganizationId == organization.Id);
    }

    IQueryable<AuditEventBase> GetQueryable(
        StatusFilter? statusFilter = StatusFilter.All,
        ActivityTypeFilter? eventTypeFilter = ActivityTypeFilter.User,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        Organization? organization = null,
        Skill? skill = null,
        SkillTrigger? trigger = null)
    {
        var queryable = GetBaseQueryable(organization);

        queryable = statusFilter switch
        {
            StatusFilter.Success => queryable.Where(e => e.ErrorMessage == null),
            StatusFilter.Error => queryable.Where(e => e.ErrorMessage != null),
            _ => queryable
        };

        if (minDate is not null)
        {
            queryable = queryable.Where(e => e.Created >= minDate.Value);
        }
        if (maxDate is not null)
        {
            queryable = queryable.Where(e => e.Created <= maxDate.Value);
        }

        if (skill is not null)
        {
            queryable = queryable.OfType<SkillAuditEvent>()
                .Where(e => e.SkillId == skill.Id);
        }

        if (trigger is not null)
        {
            queryable = queryable.OfType<LegacyAuditEvent>().Where(e => e.EntityId == trigger.Id &&
                                                                        (
                                                                            e.Discriminator.Contains(nameof(TriggerRunEvent))
                                                                            || e.Discriminator.Contains(nameof(TriggerChangeEvent))
                                                                        ));
        }

        return eventTypeFilter switch
        {
            ActivityTypeFilter.User => queryable.Where(e =>
                !e.Discriminator.Contains(nameof(TriggerRunEvent))
                && ((SkillRunAuditEvent)e).Signal == null
                && e!.Actor.IsBot != true),
            ActivityTypeFilter.All => queryable,
            ActivityTypeFilter.Installation => queryable.OfType<InstallationEvent>(),
            ActivityTypeFilter.Permission => queryable.Where(q => q.Description.Contains(" permissions for ")
                                                                  || q.Description.Contains(" permissions to ")
                                                                  || q.Description.StartsWith("Restricted skill ")
                                                                  || q.Description.StartsWith("Unrestricted skill ")
                                                                  || q.Description.StartsWith("Removed role ")
                                                                  || q.Description.StartsWith("Added role ")),
            ActivityTypeFilter.Secret => queryable.OfType<SkillSecretEvent>(),
            ActivityTypeFilter.SkillChange => queryable.OfType<SkillInfoChangedAuditEvent>(),
            ActivityTypeFilter.SkillCodeEdit => queryable.OfType<SkillEditSessionAuditEvent>(),
            ActivityTypeFilter.SkillRename => queryable.OfType<SkillInfoChangedAuditEvent>()
                .Where(q => q.ChangeType == "Renamed"),
            ActivityTypeFilter.SkillRun => queryable.OfType<SkillRunAuditEvent>(),
            ActivityTypeFilter.BuiltInSkillRun => queryable.OfType<BuiltInSkillRunEvent>(),
            ActivityTypeFilter.SkillNotFound => queryable.OfType<SkillNotFoundEvent>(),
            ActivityTypeFilter.Staff => queryable.Where(e => e.Discriminator.StartsWith("Staff")),
            ActivityTypeFilter.Trigger => queryable.OfType<TriggerChangeEvent>(),
            ActivityTypeFilter.Subscription => queryable.OfType<BillingEvent>(),
            ActivityTypeFilter.Admin => queryable.OfType<AdminAuditEvent>(),
            _ => queryable
        };
    }
}

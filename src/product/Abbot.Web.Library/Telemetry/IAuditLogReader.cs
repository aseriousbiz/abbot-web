using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Telemetry;

public interface IAuditLogReader
{
    /// <summary>
    /// Retrieves an audit entry for the specified Id. IMPORTANT: This does not filter by organization, so check
    /// access rights before showing this.
    /// </summary>
    /// <param name="id">The id of the audit entry.</param>
    Task<AuditEventBase?> GetAuditEntryAsync(Guid id);

    /// <summary>
    /// Get recent audit log entries.
    /// </summary>
    /// <param name="organization">The organization the entries belong to.</param>
    /// <param name="count">The number of items to retrieve</param>
    /// <param name="activityTypeFilter">The activity type filter to use.</param>
    /// <returns>A list of recent activity.</returns>
    Task<IReadOnlyList<AuditEventBase>> GetRecentActivityAsync(
        Organization organization,
        int count,
        ActivityTypeFilter activityTypeFilter = ActivityTypeFilter.User);

    /// <summary>
    /// Retrieve audit log entries for the full audit log.
    /// </summary>
    /// <param name="organization">The organization the entries belong to.</param>
    /// <param name="pageNumber">The current page (1-based).</param>
    /// <param name="pageSize">The number of entries on a page.</param>
    /// <param name="statusFilter">The status filter to apply.</param>
    /// <param name="activityTypeFilter">The type of event to filter on.</param>
    /// <param name="minDate">Minimum date to return.</param>
    /// <param name="maxDate">Maximum date to return.</param>
    /// <param name="includeStaff">Include staff-only events.</param>
    Task<IPaginatedList<AuditEventBase>> GetAuditEventsAsync(
        Organization organization,
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        ActivityTypeFilter activityTypeFilter,
        DateTime? minDate,
        DateTime? maxDate,
        bool includeStaff);

    /// <summary>
    /// Retrieve audit log entries for a skill.
    /// </summary>
    /// <param name="skill">The skill the entries belong to.</param>
    /// <param name="pageNumber">The current page (1-based).</param>
    /// <param name="pageSize">The number of entries on a page.</param>
    /// <param name="statusFilter">The status filter to apply.</param>
    /// <param name="skillEventFilter">The type of event to filter on.</param>
    /// <param name="minDate">Minimum date to return.</param>
    /// <param name="maxDate">Maximum date to return.</param>
    Task<IPaginatedList<AuditEventBase>> GetAuditEventsForSkillAsync(
        Skill skill,
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        SkillEventFilter skillEventFilter,
        DateTime? minDate,
        DateTime? maxDate);

    /// <summary>
    /// Retrieves audit log entries for a trigger.
    /// </summary>
    /// <param name="trigger">The trigger the entries belong to.</param>
    /// <param name="pageNumber">The current page (1-based).</param>
    /// <param name="pageSize">The number of entries on a page.</param>
    /// <param name="statusFilter">The status filter to apply.</param>
    /// <param name="minDate">Minimum date to return.</param>
    /// <param name="maxDate">Maximum date to return.</param>
    Task<IPaginatedList<AuditEventBase>> GetAuditEventsForSkillTriggerAsync(
        SkillTrigger trigger,
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        DateTime? minDate,
        DateTime? maxDate);

    /// <summary>
    /// Retrieve audit log entries for the Staff Tools audit log.
    /// </summary>
    /// <param name="pageNumber">The current page (1-based).</param>
    /// <param name="pageSize">The number of entries on a page.</param>
    /// <param name="statusFilter">The status filter to apply.</param>
    /// <param name="activityTypeFilter">The type of event to filter on.</param>
    /// <param name="minDate">Minimum date to return.</param>
    /// <param name="maxDate">Maximum date to return.</param>
    Task<IPaginatedList<AuditEventBase>> GetAuditEventsForStaffAsync(
        int pageNumber,
        int pageSize,
        StatusFilter statusFilter,
        ActivityTypeFilter activityTypeFilter,
        DateTime? minDate,
        DateTime? maxDate);

    /// <summary>
    /// Retrieves audit log entries that are all part of the same logical operation based on the trace id.
    /// </summary>
    /// <param name="parentEvent">The parent event to retrieve related events for.</param>
    /// <returns>A list of <see cref="AuditEventBase"/> with the same <see cref="AuditEventBase.TraceId"/> or a <see cref="AuditEventBase.ParentIdentifier"/> that matches the provided event's <see cref="AuditEventBase.Identifier"/>.</returns>
    Task<IReadOnlyList<AuditEventBase>> GetRelatedEventsAsync(AuditEventBase parentEvent);
}

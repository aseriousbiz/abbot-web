using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Activity;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Activity;

public class IndexPage : ActivityPageBase
{
    public IndexPage(IAuditLogReader auditLog, IUserRepository userRepository) : base(auditLog, userRepository)
    {
        StaffMode = true;
    }

    public Task OnGetAsync(
        int? p = null,
        StatusFilter? filter = null,
        ActivityTypeFilter? type = null,
        string? range = null)
    {
        return GetAsync(
            p ?? 1,
            filter ?? StatusFilter.All,
            type ?? ActivityTypeFilter.All,
            range ?? string.Empty);
    }

    protected override Task<IPaginatedList<AuditEventBase>> LoadActivityEventsAsync(
        Organization organization,
        DateTime? minDate,
        DateTime? maxDate)
    {
        if (!HttpContext.IsStaffMode())
        {
            throw new InvalidOperationException("User must be a staff user");
        }

        return AuditLog.GetAuditEventsForStaffAsync(
            PageNumber,
            PageCount.GetValueOrDefault(),
            Filter,
            Type,
            minDate,
            maxDate);
    }
}

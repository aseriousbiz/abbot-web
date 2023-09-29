using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Activity;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Organizations.Activity;

public class IndexPage : ActivityPageBase
{
    readonly AbbotContext _db;

    public new Organization Organization { get; private set; } = null!;

    public IndexPage(AbbotContext db, IAuditLogReader auditLog, IUserRepository userRepository) : base(auditLog, userRepository)
    {
        _db = db;
    }

    public async Task OnGetAsync(
        string orgId,
        int? p = null,
        StatusFilter? filter = null,
        ActivityTypeFilter? type = null,
        string? range = null)
    {
        if (!HttpContext.IsStaffMode())
        {
            throw new InvalidOperationException("User must be a staff user");
        }

        Organization = await _db.Organizations
#pragma warning disable CA1304
            .Include(o => o.Members)
            .ThenInclude(u => u.MemberRoles)
            .ThenInclude(ur => ur.Role)
            .Include(o => o.Skills)
            .SingleAsync(o => o.PlatformId.ToLower() == orgId.ToLower());
#pragma warning restore CA1304
        ViewData.SetOrganization(Organization);

        await GetAsync(
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
        return AuditLog.GetAuditEventsAsync(
            Organization, // Ignore the passed in organization.
            PageNumber,
            PageCount.GetValueOrDefault(),
            Filter,
            Type,
            minDate,
            maxDate,
            true); // This is a staff page, so we can always show staff events.
    }
}

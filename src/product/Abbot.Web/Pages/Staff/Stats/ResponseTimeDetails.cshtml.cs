using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Pages.Staff.Stats;

public class ResponseTimeDetailsPage : StaffToolsPage
{
    readonly AbbotContext _db;

    public ResponseTimeDetailsPage(AbbotContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Organization> OrganizationsWithResponseTimes { get; private set; } = Array.Empty<Organization>();

    public BasicStatistics? DefaultWarningResponseTimeStats { get; private set; }

    public BasicStatistics? DefaultDeadlineResponseTimeStats { get; private set; }

    public BasicStatistics? WarningResponseTimeStats { get; private set; }

    public BasicStatistics? DeadlineResponseTimeStats { get; private set; }


    public async Task OnGetAsync()
    {
        OrganizationsWithResponseTimes = await GetOrganizationsNotUsQueryable()
            .Where(o => o.DefaultTimeToRespond.Warning != null || o.DefaultTimeToRespond.Deadline != null)
            .ToListAsync();

        var warningTimespans = OrganizationsWithResponseTimes
            .Where(o => o.DefaultTimeToRespond.Warning != null)
            .Select(o => o.DefaultTimeToRespond.Warning.GetValueOrDefault().TotalSeconds)
            .ToList();

        DefaultWarningResponseTimeStats = warningTimespans.CalculateBasicStatistics();

        var deadlineTimespans = OrganizationsWithResponseTimes
            .Where(o => o.DefaultTimeToRespond.Deadline != null)
            .Select(o => o.DefaultTimeToRespond.Deadline.GetValueOrDefault().TotalSeconds)
            .ToList();

        DefaultDeadlineResponseTimeStats = deadlineTimespans.CalculateBasicStatistics();

        var roomResponseTimes = await _db.Rooms
            .Where(r => r.TimeToRespond.Warning != null || r.TimeToRespond.Deadline != null)
            .ToListAsync();

        WarningResponseTimeStats = roomResponseTimes
            .Where(r => r.TimeToRespond.Warning != null)
            .Select(r => r.TimeToRespond.Warning.GetValueOrDefault().TotalSeconds)
            .CalculateBasicStatistics();

        DeadlineResponseTimeStats = roomResponseTimes
            .Where(r => r.TimeToRespond.Deadline != null)
            .Select(r => r.TimeToRespond.Deadline.GetValueOrDefault().TotalSeconds)
            .CalculateBasicStatistics();
    }

    // HACK: Temporary way to identify our own organizations
    static readonly int[] SeriousOrganizations = { 1, 21, 35, 51, 58, 158 };

    IQueryable<Organization> GetOrganizationsNotUsQueryable() =>
        _db.Organizations.Where(o => o.PlanType != PlanType.None)
            .Where(o => !SeriousOrganizations.Contains(o.Id));
}


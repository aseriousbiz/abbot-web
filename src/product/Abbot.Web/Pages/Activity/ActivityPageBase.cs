using System.Globalization;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Pages.Activity;

public abstract class ActivityPageBase : UserPage
{
    protected ActivityPageBase(IAuditLogReader auditLog, IUserRepository userRepository)
    {
        AuditLog = auditLog;
        UserRepository = userRepository;
        ShowEventTypeFilter = true;
    }

    public bool ShowEventTypeFilter { get; set; }

    public bool ShowSkillEventTypeFilter { get; set; }

    public IPaginatedList<AuditEventBase> ActivityEvents { get; private set; } = null!;
    public int PageNumber { get; private set; }

    public int? PageCount { get; set; } = 25;

    public StatusFilter Filter { get; private set; }

    public ActivityTypeFilter Type { get; private set; }

    public string Range { get; private set; } = null!;

    public bool StaffMode { get; protected set; }

    public Member Abbot { get; private set; } = null!;

    protected IAuditLogReader AuditLog { get; }
    public IUserRepository UserRepository { get; }

    protected async Task GetAsync(
        int p,
        StatusFilter filter,
        ActivityTypeFilter type,
        string range)
    {
        Filter = filter;
        Type = type;
        Range = range;
        PageNumber = p;

        var (minDate, maxDate) = ParseDateRange(Range);
        Abbot = await UserRepository.EnsureAbbotMemberAsync(Organization);
        ActivityEvents = await LoadActivityEventsAsync(Organization, minDate, maxDate);
    }

    protected virtual Task<IPaginatedList<AuditEventBase>> LoadActivityEventsAsync(Organization organization,
        DateTime? minDate,
        DateTime? maxDate)
    {
        return AuditLog.GetAuditEventsAsync(
            organization ?? throw new InvalidOperationException("No organization found."),
            PageNumber,
            PageCount.GetValueOrDefault(),
            Filter,
            Type,
            minDate,
            maxDate,
            false); // Only show staff-only events on the staff tools page.
    }

    static (DateTime?, DateTime?) ParseDateRange(string dateRange)
    {
        if (dateRange is not { Length: > 0 })
        {
            return (null, null);
        }

        var dates = dateRange.Split(" to ");
        DateTime? secondDate = null;
        if (dates.Length == 2)
        {
            secondDate = DateTime.Parse(dates[1], CultureInfo.InvariantCulture).ToUniversalTime();
        }

        var firstDate = DateTime.Parse(dates[0], CultureInfo.InvariantCulture).ToUniversalTime();
        return (firstDate, secondDate);
    }
}

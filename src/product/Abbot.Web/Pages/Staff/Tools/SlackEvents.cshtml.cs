using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Staff.Tools;

public class SlackEventsPage : StaffToolsPage
{
    readonly AbbotContext _db;

    public SlackEventsPage(AbbotContext abbotContext)
    {
        _db = abbotContext;
    }

    public IReadOnlyList<SlackEvent> RecentIncomplete { get; private set; } = null!;

    public SlackEventStats Stats { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        var daysCount = await _db.SlackEventsRollups.CountAsync();
        var totalSuccess = await _db.SlackEventsRollups.SumAsync(r => r.SuccessCount);
        var totalIncomplete = await _db.SlackEventsRollups.SumAsync(r => r.IncompleteCount);

        var total = totalSuccess + totalIncomplete;
        var dailyAverage = daysCount > 0
            ? total / daysCount
            : 0;
        var dailyErrorAverage = daysCount > 0
            ? (double)totalIncomplete / daysCount
            : 0;

        Stats = new SlackEventStats(total, totalIncomplete, dailyAverage, dailyErrorAverage);

        RecentIncomplete = await _db.SlackEvents
            .OrderByDescending(e => e.Created)
            .Where(e => e.Error == null)
            .Where(e => e.Completed == null)
            .Take(10)
            .ToListAsync();
        return Page();
    }

    public IActionResult OnPostAsync(string id)
    {
        return RedirectToPage("Details", new { id });
    }

    public readonly record struct SlackEventStats(
        long Total,
        long TotalIncomplete,
        double DailyAverage,
        double DailyErrorAverage);
}

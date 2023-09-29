using System.ComponentModel;
using System.Linq;
using System.Threading;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class DailySlackEventsRollupJob : IRecurringJob
{
    readonly AbbotContext _db;

    public DailySlackEventsRollupJob(AbbotContext db)
    {
        _db = db;
    }

    public static string Name => "Abbot Daily Slack Events Rollup";

    [DisplayName("Roll Up SlackEvents")]
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var lastRollupDate =
            (await _db.SlackEventsRollups.OrderBy(r => r.Date).FirstOrDefaultAsync(cancellationToken))?.Date.ToUniversalTime()
            ?? (await _db.SlackEvents.OrderBy(o => o.Created).FirstOrDefaultAsync(cancellationToken))?.Created.Date.ToUniversalTime();

        if (lastRollupDate is null) // This would only happen when there are no SlackEvents to roll-up.
        {
            return;
        }
        var endDate = DateTime.UtcNow.Date;
        var rollupDate = lastRollupDate.Value;
        while (rollupDate < endDate) // Never do today.
        {
            await RollUpAsync(rollupDate, cancellationToken);
            rollupDate = rollupDate.AddDays(1);
        }
    }

    /// <summary>
    /// Calculates the daily rollup for the specified day.
    /// </summary>
    async Task RollUpAsync(DateTime startDate, CancellationToken cancellationToken)
    {
        startDate = startDate.Date;
        var endDate = startDate.AddDays(1);

        // If the rollup already exists, recalculate it.
        var existing = await _db.SlackEventsRollups.Where(r => r.Date == startDate).ToListAsync(cancellationToken);
        if (existing.Any())
        {
            _db.SlackEventsRollups.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var rollups = await _db.SlackEvents
            .Where(e => e.Created >= startDate && e.Created < endDate)
            .GroupBy(
                e => new { e.TeamId, e.EventType, e.Created.Date },
                (k, g) => new SlackEventsRollup
                {
                    Date = k.Date,
                    TeamId = k.TeamId,
                    EventType = k.EventType,
                    SuccessCount = g.Count(e => e.Error == null && e.Completed != null),
                    ErrorCount = g.Count(e => e.Error != null),
                    IncompleteCount = g.Count(e => e.Error == null && e.Completed == null),
                })
            .ToListAsync(cancellationToken);

        await _db.SlackEventsRollups.AddRangeAsync(rollups, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

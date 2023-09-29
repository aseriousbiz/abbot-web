using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class DailySlackEventsCleanupJob : IRecurringJob
{
    readonly AbbotContext _db;

    public static string Name => "Abbot Daily Slack Events Cleanup";

    public DailySlackEventsCleanupJob(AbbotContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Deletes all Slack events 4 or more days ago.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [Queue(HangfireQueueNames.Maintenance)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var oldSlackEventsCount = await _db.SlackEvents.CountAsync(e => e.Created < DateTime.UtcNow.AddDays(-4), cancellationToken);

        while (oldSlackEventsCount > 0)
        {
            const string sql = """DELETE FROM "SlackEvents" WHERE "Id" IN (SELECT "Id" FROM "SlackEvents" WHERE "Created" < NOW() - INTERVAL '4 days' ORDER BY "Created" LIMIT 500);""";

            await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            oldSlackEventsCount -= 500;
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Cryptography;
using Serious.TestHelpers;
using Xunit;

public class DailySlackEventsRollupJobTests
{
    static void AssertRollup(
        DateTime expectedDate,
        string expectedTeamId,
        string expectedEventType,
        int expectedSuccessCount,
        int expectedErrorCount,
        SlackEventsRollup actual)
    {
        Assert.Equal(expectedDate, actual.Date);
        Assert.Equal(expectedTeamId, actual.TeamId);
        Assert.Equal(expectedEventType, actual.EventType);
        Assert.Equal(expectedSuccessCount, actual.SuccessCount);
        Assert.Equal(expectedErrorCount, actual.ErrorCount);
    }

    public class TheRunAsyncMethod
    {
        readonly DateTime _today = DateTime.UtcNow.Date;

        [Fact]
        public async Task RollsUpAllDaysSinceLastRollup()
        {
            var db = new FakeAbbotContext();
            var lastRollup = new SlackEventsRollup
            {
                EventType = "message",
                TeamId = "T08675309",
                Date = _today.AddDays(-3).Date
            };
            await db.SlackEventsRollups.AddAsync(lastRollup);
            // Add some data to roll-up.
            await db.SlackEvents.AddRangeAsync(new[]
            {
                Create("T00000001", "message", 2),
                Create("T00000001", "message", 2),
                Create("T08675309", "app_mention", 2),
                Create("T08675309", "message", 2, "shit broke"),
                Create("T08675309", "message", 2),
                Create("T08675309", "message", 2),
                Create("T08675309", "message", 2),
                Create("T00000001", "app_mention", 1, "also broke"),
                Create("T00000001", "message", 1),
                Create("T00000001", "message", 1),
                Create("T00000001", "message", 1, "it broke again"),
                Create("T08675309", "app_mention", 1),
                Create("T08675309", "app_mention", 1),
                Create("T08675309", "message", 1),
                Create("T08675309", "message", 1),
                Create("T08675309", "message", 1),
            });
            await db.SaveChangesAsync();
            var job = new DailySlackEventsRollupJob(db);

            await job.RunAsync();

            var metrics = await db.SlackEventsRollups.OrderBy(r => r.Date)
                .ThenBy(r => r.TeamId)
                .ThenBy(r => r.EventType)
                .ToListAsync();
            Assert.Equal(7, metrics.Count);
            var twoDaysAgo = _today.AddDays(-2);
            AssertRollup(twoDaysAgo, "T00000001", "message", 2, 0, metrics[0]);
            AssertRollup(twoDaysAgo, "T08675309", "app_mention", 1, 0, metrics[1]);
            AssertRollup(twoDaysAgo, "T08675309", "message", 3, 1, metrics[2]);
            var oneDayAgo = twoDaysAgo.AddDays(1);
            AssertRollup(oneDayAgo, "T00000001", "app_mention", 0, 1, metrics[3]);
            AssertRollup(oneDayAgo, "T00000001", "message", 2, 1, metrics[4]);
            AssertRollup(oneDayAgo, "T08675309", "app_mention", 2, 0, metrics[5]);
            AssertRollup(oneDayAgo, "T08675309", "message", 3, 0, metrics[6]);
        }

        [Fact]
        public async Task RollsUpAllSlackEventsWhenNoRollupsExist()
        {
            var db = new FakeAbbotContext();
            // Add some data to roll-up.
            await db.SlackEvents.AddRangeAsync
            (
                Create("T00000001", "message", 2),
                Create("T00000001", "message", 2),
                Create("T08675309", "app_mention", 2),
                Create("T08675309", "message", 2, "shit broke"),
                Create("T08675309", "message", 2),
                Create("T08675309", "message", 2),
                Create("T08675309", "message", 2),
                Create("T00000001", "app_mention", 1, "also broke"),
                Create("T00000001", "message", 1),
                Create("T00000001", "message", 1),
                Create("T00000001", "message", 1, "it broke again"),
                Create("T08675309", "app_mention", 1),
                Create("T08675309", "app_mention", 1),
                Create("T08675309", "message", 1),
                Create("T08675309", "message", 1),
                Create("T08675309", "message", 1)
            );
            await db.SaveChangesAsync();
            var job = new DailySlackEventsRollupJob(db);

            await job.RunAsync();

            var metrics = await db.SlackEventsRollups.OrderBy(r => r.Date)
                .ThenBy(r => r.TeamId)
                .ThenBy(r => r.EventType)
                .ToListAsync();
            Assert.Equal(7, metrics.Count);
            var twoDaysAgo = _today.AddDays(-2);
            AssertRollup(twoDaysAgo, "T00000001", "message", 2, 0, metrics[0]);
            AssertRollup(twoDaysAgo, "T08675309", "app_mention", 1, 0, metrics[1]);
            AssertRollup(twoDaysAgo, "T08675309", "message", 3, 1, metrics[2]);
            var oneDayAgo = twoDaysAgo.AddDays(1);
            AssertRollup(oneDayAgo, "T00000001", "app_mention", 0, 1, metrics[3]);
            AssertRollup(oneDayAgo, "T00000001", "message", 2, 1, metrics[4]);
            AssertRollup(oneDayAgo, "T08675309", "app_mention", 2, 0, metrics[5]);
            AssertRollup(oneDayAgo, "T08675309", "message", 3, 0, metrics[6]);
        }

        [Fact]
        public async Task DoesNothingIfNoRecords()
        {
            var db = new FakeAbbotContext();
            var job = new DailySlackEventsRollupJob(db);

            await job.RunAsync();

            var metrics = await db.DailyMetricsRollups.ToListAsync();
            Assert.Empty(metrics);
        }

        static int _hour = 2;
        static int _minute = 1;
        static int _seconds = 11;

        SlackEvent Create(string teamId, string eventType, int daysAgo, string? error = null)
        {
            var hour = _hour++ % 24;
            var minute = _minute++ % 60;
            var seconds = _seconds++ % 60;
            var created = _today.AddDays(-1 * daysAgo).Date.AddHours(hour).AddMinutes(minute).AddSeconds(seconds);
            var completed = created.AddSeconds(1);
            return new SlackEvent
            {
                EventId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                EventType = eventType,
                Created = _today.AddDays(-1 * daysAgo).Date.AddHours(hour).AddMinutes(minute).AddSeconds(seconds),
                Content = new SecretString("{}", new FakeDataProtectionProvider()),
                Error = error,
                Completed = completed,
                AppId = "A0123456",
            };
        }
    }
}

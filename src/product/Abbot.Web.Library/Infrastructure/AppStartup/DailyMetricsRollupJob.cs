using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Job to roll up much of our daily metrics into the DailyMetricsRollup table.
/// </summary>
public class DailyMetricsRollupJob : IRecurringJob
{
    readonly AbbotContext _db;

    public DailyMetricsRollupJob(AbbotContext db)
    {
        _db = db;
    }

    public static string Name => "Abbot Daily Metrics Rollup";

    [DisplayName("Roll Up To Today")]
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var lastRollupDate =
            (await _db.DailyMetricsRollups.OrderBy(r => r.Date).FirstOrDefaultAsync(cancellationToken))?.Date.ToUniversalTime()
            ?? (await _db.Users.OrderBy(o => o.Created).FirstOrDefaultAsync(cancellationToken))?.Created.Date.ToUniversalTime();

        if (lastRollupDate is null) // This would only happen on a brand new database.
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
        var rollup = await _db.DailyMetricsRollups.FindAsync(new object[] { startDate }, cancellationToken);
        if (rollup is null)
        {
            rollup = new DailyMetricsRollup
            {
                Date = startDate
            };
            await _db.DailyMetricsRollups.AddAsync(rollup, cancellationToken);
        }

        rollup.ActiveUserCount = await GetHumanAuditEvents()
            .Where(e => e.Created >= startDate && e.Created < endDate)
            .GroupBy(e => e.ActorId)
            .Select(e => e.Key)
            .CountAsync(cancellationToken);

        rollup.InteractionCount = await GetHumanAuditEvents()
            .Where(e => e.Created >= startDate && e.Created < endDate)
            .CountAsync(cancellationToken);

        rollup.SkillCreatedCount = await _db.Skills
            .Include(s => s.Organization)
            .Where(e => e.Created >= startDate && e.Created < endDate)
            .CountAsync(cancellationToken);

        rollup.OrganizationCreatedCount = await _db.Organizations
            .Where(o => o.Created >= startDate && o.Created < endDate)
            .CountAsync(cancellationToken);

        rollup.UserCreatedCount = await _db.Users
            .Where(u => u.NameIdentifier != null && u.Created >= startDate && u.Created < endDate)
            .CountAsync(cancellationToken);

        rollup.MonthlyRecurringRevenue = await GetCurrentMRRAsync(cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    IQueryable<AuditEventBase> GetHumanAuditEvents() =>
        _db.AuditEvents
            .Include(e => e.Organization)
            .Where(a => !(a is TriggerRunEvent));

    // ReSharper disable once InconsistentNaming
    async Task<decimal> GetCurrentMRRAsync(CancellationToken cancellationToken)
    {
        var payingCustomers = await _db.Organizations
            .Where(o => o.StripeSubscriptionId != null)
            .ToListAsync(cancellationToken);
        return payingCustomers.Sum(o => o.GetPlan().MonthlyPrice);
    }
}

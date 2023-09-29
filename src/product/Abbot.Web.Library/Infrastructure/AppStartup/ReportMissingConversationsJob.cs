using System.Linq;
using System.Threading;
using Hangfire;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Job used to ensure that conversations that should be tracked are tracked.
/// </summary>
public class ReportMissingConversationsJob : IRecurringJob
{
    const string JobName = "Report Missing Conversations";

    readonly AbbotContext _db;
    readonly IMissingConversationsReporter _missingConversationsReporter;
    // We inject this (instead of using the static one), because this logging is an essential part of what this class does and we need to be able to test it.
    readonly ILogger<ReportMissingConversationsJob> _log;
    readonly IClock _clock;
    readonly IStopwatchFactory _stopwatchFactory;

    public static string Name => JobName;

    public ReportMissingConversationsJob(
        AbbotContext db,
        IMissingConversationsReporter missingConversationsReporter,
        ILogger<ReportMissingConversationsJob> log,
        IClock clock,
        IStopwatchFactory stopwatchFactory)
    {
        _db = db;
        _missingConversationsReporter = missingConversationsReporter;
        _log = log;
        _clock = clock;
        _stopwatchFactory = stopwatchFactory;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 10)] // Only allow one instance to be running at a time.
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = _stopwatchFactory.StartNew();

        try
        {
            await FixOrganizationsAsync(stopwatch, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _log.JobCanceled(ex, elapsedHumanized: stopwatch.Elapsed.Humanize());
        }
    }

    async Task FixOrganizationsAsync(IStopwatch stopwatch, CancellationToken cancellationToken)
    {
        // Grab every Business Plan or Free Trial organization with tracked rooms.
        var organizations = await _db.Organizations
            .Include(o => o.Rooms)
            .Where(r => r.PlatformType == PlatformType.Slack)
            .Where(o => o.ApiToken != null)
            .Where(o => o.Enabled == true)
            .Where(o => o.PlanType == PlanType.Business || o.Trial != null && o.Trial.Expiry > _clock.UtcNow)
            .Where(o => o.Rooms!.Any(r =>
                r.ManagedConversationsEnabled
                && r.BotIsMember != false
                && r.Deleted != true
                && r.Archived != true))
            .OrderByDescending(r => r.PurchasedSeatCount)
            .ToListAsync(cancellationToken);

        // Double check that we have an organization that's allowed to have managed conversations.
        var organizationsWithConversationTrackingPlans = organizations
            .Where(o => o.HasPlanFeature(PlanFeature.ConversationTracking));

        int completedCount = 0;
        try
        {
            foreach (var organization in organizationsWithConversationTrackingPlans)
            {
                using var orgScope = _log.BeginOrganizationScope(organization);
                await _missingConversationsReporter.LogUntrackedConversationsAsync(organization, cancellationToken);
                completedCount++;
            }
            _log.JobCompleted(organizations.Count, elapsedHumanized: stopwatch.Elapsed.Humanize());
        }
        catch (OperationCanceledException exception)
        {
            _log.JobCancelledMidway(
                exception,
                completedCount,
                organizations.Count,
                elapsedHumanized: stopwatch.Elapsed.Humanize());
        }
    }
}

public static partial class ReportMissingConversationsJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Repaired conversations in {OrganizationCount} organizations completed in {ElapsedHumanized}.")]
    public static partial void JobCompleted(
        this ILogger<ReportMissingConversationsJob> logger,
        int organizationCount,
        string elapsedHumanized);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Repairing conversations job cancelled after {ElapsedHumanized}.")]
    public static partial void JobCanceled(
        this ILogger<ReportMissingConversationsJob> logger,
        OperationCanceledException exception,
        string elapsedHumanized);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Repairing conversations job cancelled after completing {CompletedCount} of {OrganizationCount} organizations in {ElapsedHumanized}.")]
    public static partial void JobCancelledMidway(
        this ILogger<ReportMissingConversationsJob> logger,
        OperationCanceledException exception,
        int completedCount,
        int organizationCount,
        string elapsedHumanized);
}

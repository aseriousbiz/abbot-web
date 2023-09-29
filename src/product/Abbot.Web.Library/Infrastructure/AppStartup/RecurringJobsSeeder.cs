using System.Threading;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Compilation;
using Serious.Abbot.Eventing;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// A collection of cron expressions for use with <see cref="IRecurringJobManager"/>.
/// </summary>
public static class CronExpressions
{
    /// <summary>
    /// Returns a cron expression that runs every <paramref name="minutes"/> minutes.
    /// </summary>
    /// <remarks>
    /// <see cref="Cron"/> has a method of this name, but it's obsolete and will be removed. However, I think it's
    /// on the right path in helping us avoid mistakes like I've made in the past - @haacked
    /// </remarks>
    /// <param name="minutes">The number of minutes in the interval.</param>
    /// <returns>A cron expression as a string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the minutes is less than 1 or greater than 59.</exception>
    public static string MinuteInterval(int minutes)
    {
        if (minutes is < 1 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Must be between 1 and 59.");
        }
        return $"*/{minutes} * * * *";
    }

    /// <summary>
    /// Returns a cron expression that runs every <paramref name="minutes"/> minutes.
    /// </summary>
    /// <remarks>
    /// <see cref="Cron"/> has a method of this name, but it's obsolete and will be removed. However, I think it's
    /// on the right path in helping us avoid mistakes like I've made in the past - @haacked
    /// </remarks>
    /// <param name="minutes">The number of minutes in the interval.</param>
    /// <param name="offset">The offset in which to start the schedule. For example, 5 means start at the 5 minute mark and then run every <paramref name="minutes"/> minute.</param>
    /// <returns>A cron expression as a string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the minutes is less than 1 or greater than 59.</exception>
    public static string MinuteInterval(int minutes, int offset)
    {
        if (minutes is < 1 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Must be between 1 and 59.");
        }
        if (offset is < 1 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Must be between 1 and 59.");
        }
        return $"{offset}/{minutes} * * * *";
    }
}

/// <summary>
/// Ensures the daily assembly garbage collection job is scheduled to run daily around 3 AM PDT.
/// This calls <see cref="AssemblyCacheGarbageCollectionJob.CollectAsync"/> to do the collection.
/// </summary>
public sealed class RecurringJobsSeeder : IDataSeeder
{
    static readonly ILogger<RecurringJobsSeeder> Log = ApplicationLoggerFactory.CreateLogger<RecurringJobsSeeder>();

    readonly IRecurringJobManager _recurringJobManager;
    readonly IHostEnvironment _hostEnvironment;
    readonly IOptions<EventingOptions> _eventingOptions;
    readonly IOptions<JobsOptions> _jobsOptions;

    public RecurringJobsSeeder(IRecurringJobManager recurringJobManager, IHostEnvironment hostEnvironment, IOptions<EventingOptions> eventingOptions, IOptions<JobsOptions> jobsOptions)
    {
        _recurringJobManager = recurringJobManager;
        _hostEnvironment = hostEnvironment;
        _eventingOptions = eventingOptions;
        _jobsOptions = jobsOptions;
    }

    /// <summary>
    /// Creates or updates the "AssemblyGarbageCollection" job.
    /// </summary>
    public Task SeedDataAsync()
    {
        AddOrUpdateRecurringJob<AssemblyCacheGarbageCollectionJob>(
            Cron.Daily(10)); // 3 AM PDT

        AddOrUpdateRecurringJob<DailyMetricsRollupJob>(
            Cron.Daily(1)); // 1:00 AM every day.

        AddOrUpdateRecurringJob<DailySlackEventsRollupJob>(Cron.Daily(1, 15)); // 1:15 AM every day.

        // 12:01 PM UTC every day (4:01 AM PT, 8:00 PM Singapore).
        AddOrUpdateRecurringJob<DailySlackEventsCleanupJob>(Cron.Daily(12, 01));

        AddOrUpdateRecurringJob<ConversationExpirationJob>(
            Cron.Minutely()); // Every minute.

        AddOrUpdateRecurringJob<RespondersNotificationJob>(
            CronExpressions.MinuteInterval(30, 10)); // Every 30 minutes starting at the 10 minute mark.

        AddOrUpdateRecurringJob<ExpireTrialsJob>(
            Cron.Daily(2)); // 2:00 AM every day.

        AddOrUpdateRecurringJob<UpdateOrganizationsFromSlackApiJob>(
            _hostEnvironment.IsDevelopment()
                ? Cron.Weekly(DayOfWeek.Friday) // Every week on Friday (in Dev).
                : Cron.Hourly(42)); // Every hour on the 42nd minute (in Prod).

        AddOrUpdateRecurringJob<ReportMissingConversationsJob>(
            Cron.Daily(3)); // 3:00 AM every day.

        AddOrUpdateRecurringJob<AnnouncementsCompletionJob>(
            Cron.Minutely());

        AddOrUpdateRecurringJob<SettingsCleanupJob>(
            Cron.Hourly());

        if (_jobsOptions.Value.BotMembershipJobEnabled)
        {
            AddOrUpdateRecurringJob<BotMembershipJob>(
                _hostEnvironment.IsDevelopment()
                    ? Cron.Weekly(DayOfWeek.Friday) // Every week on Friday (in Dev).
                    : Cron.Hourly(15)); // Every hour on the 15th minute (in Prod).
        }
        else
        {
            _recurringJobManager.RemoveIfExists(BotMembershipJob.Name);
        }

        if (_eventingOptions.Value.Transport == EventingOptions.AzureServiceBusTransport)
        {
            AddOrUpdateRecurringJob<ServiceBusSubscriptionCleanupJob>(
                Cron.Daily(hour: 12)); // Every day at noon UTC (4/5 AM PT depending on daylight saving)
        }

        return Task.CompletedTask;
    }

    void AddOrUpdateRecurringJob<T>(string cronExpression)
        where T : IRecurringJob
    {
        Log.AttemptToCreateRecurringJob(T.Name);
        _recurringJobManager.AddOrUpdate<T>(
            T.Name,
            job => job.RunAsync(default),
            cronExpression);
    }

    public bool Enabled => true;
}

/// <summary>
/// A recurring job that runs on a schedule.
/// </summary>
public interface IRecurringJob
{
    /// <summary>
    /// The name of the recurring job.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Runs the recurring job.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RunAsync(CancellationToken cancellationToken = default);
}

public interface IStartupJob : IRecurringJob
{
    string CronSchedule { get; }
}

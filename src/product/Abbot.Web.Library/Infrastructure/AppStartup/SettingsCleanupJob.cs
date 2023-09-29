using System.Threading;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class SettingsCleanupJob : IRecurringJob
{
    static readonly ILogger<SettingsCleanupJob> Log = ApplicationLoggerFactory.CreateLogger<SettingsCleanupJob>();

    readonly ISettingsManager _settingsManager;

    public SettingsCleanupJob(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public static string Name => "Clean Expired Settings";

    [Queue(HangfireQueueNames.Maintenance)]
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Be conservative. Don't purge settings that are less than half and hour past expiry.
        var deleted = await _settingsManager.RemoveExpiredSettingsAsync(
            DateTime.UtcNow.AddMinutes(-30),
            cancellationToken);
        Log.PurgedSettings(deleted);
    }
}

public static partial class SettingCleanupJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Purged {Count} expired settings.")]
    public static partial void PurgedSettings(
        this ILogger<SettingsCleanupJob> logger,
        int count);
}

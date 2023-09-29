using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class DataSeedRunnerService : IHostedService, IDisposable
{
    readonly IServiceScopeFactory _serviceScopeFactory;
    Task? _backgroundSeedersTask;
    CancellationTokenSource _shutdownSource = new CancellationTokenSource();

    public DataSeedRunnerService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var serviceScope = _serviceScopeFactory.CreateScope();
        var dataSeedRunner = serviceScope.ServiceProvider.GetRequiredService<DataSeedRunner>();
        await dataSeedRunner.SeedDataAsync(runBlockingSeeders: true, cancellationToken: cancellationToken);

        // Now, kick off a background task to run the "non-blocking" seeders.
        // We don't await it though, unless we're shutting down
        _backgroundSeedersTask = RunBackgroundSeedersAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdownSource.Cancel();
        var localTask = Volatile.Read(ref _backgroundSeedersTask);
        if (localTask != null)
        {
            await localTask;
        }
    }

    async Task RunBackgroundSeedersAsync()
    {
        using var serviceScope = _serviceScopeFactory.CreateScope();
        var dataSeedRunner = serviceScope.ServiceProvider.GetRequiredService<DataSeedRunner>();
        await dataSeedRunner.SeedDataAsync(runBlockingSeeders: false, cancellationToken: _shutdownSource.Token);
    }

    public void Dispose()
    {
        _shutdownSource.Dispose();
    }
}

/// <summary>
/// When the app starts up, runs all the <see cref="IDataSeeder"/> instances
/// to seed data and make data changes as needed.
/// </summary>
[SuppressMessage("Microsoft.Performance", "CA1812", Justification = "Class is created via reflection")]
public class DataSeedRunner
{
    readonly ISettingsManager _settingsManager;
    readonly IUserRepository _userRepository;
    static readonly ILogger<DataSeedRunner> Log = ApplicationLoggerFactory.CreateLogger<DataSeedRunner>();

    static readonly Histogram<long> AllSeedersDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "startup.allSeeders.duration",
        "milliseconds",
        "The duration of all data seeders");

    static readonly Histogram<long> SeederDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "startup.seeder.duration",
        "milliseconds",
        "The duration of a specific data seeder");

    public DataSeedRunner(
        IEnumerable<IDataSeeder> dataSeeders,
        ISettingsManager settingsManager,
        IUserRepository userRepository)
    {
        _settingsManager = settingsManager;
        _userRepository = userRepository;
        DataSeeders = dataSeeders.Where(ds => ds.Enabled).ToReadOnlyList();
    }

    public IReadOnlyList<IDataSeeder> DataSeeders { get; }

    /// <summary>
    /// Runs <see cref="IDataSeeder.SeedDataAsync"/> on all of the registered
    /// <see cref="IDataSeeder"/> instances in dependency order.
    /// </summary>
    public async Task SeedDataAsync(bool runBlockingSeeders, CancellationToken cancellationToken)
    {
        using var allSeederTimer = AllSeedersDuration.Time();

        var actor = await _userRepository.EnsureAbbotUserAsync();

        var dataSeeders = runBlockingSeeders
            ? DataSeeders.Where(d => d.BlockServerStartup).ToList()
            : DataSeeders.Where(d => !d.BlockServerStartup).ToList();

        Log.AboutToRunSeeders(dataSeeders.Count, runBlockingSeeders ? "Blocking" : "NonBlocking");

        // TODO: Once we're running this code, and all the seeder settings have a common prefix,
        // we could change to loading the list of completed Run-Once Seeders in a single query

        foreach (var seeder in dataSeeders)
        {
            try
            {
                if (seeder is IRunOnceDataSeeder runOnceDataSeeder && await HasRunAsync(runOnceDataSeeder, actor))
                {
                    // This seeder has already run, so skip.
                    continue;
                }

                Log.RunningSeeder(seeder.GetType());
                using var seederTimer = SeederDuration.Time(
                    new TagList()
                    {
                        { "SeederType", seeder.GetType().FullName },
                    });
                var seederStopwatch = Stopwatch.StartNew();
                await seeder.SeedDataAsync();

                Log.SeederComplete(seeder.GetType());

                if (seeder is IRunOnceDataSeeder runOnceDataSeederAfter)
                {
                    await SetRunAsync(runOnceDataSeederAfter, actor);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // Don't need to log this as an exception running a seeder.
                // But do need to re-throw so that the caller knows to stop.
                throw;
            }
            catch (Exception e)
            {
                Log.ExceptionRunningDataSeeder(e, seeder.GetType());
            }
        }
    }

    async Task<bool> HasRunAsync(IRunOnceDataSeeder dataSeeder, User actor)
    {
        var key = GetSeederKey(dataSeeder);
        var setting = await _settingsManager.GetAsync(SettingsScope.Global, key);
        if (setting is not null)
        {
            return bool.Parse(setting.Value);
        }

        var legacyKey = $"{dataSeeder.GetType().FullName}|{dataSeeder.Version}";
        var legacyValue = await _settingsManager.GetAsync(SettingsScope.Global, legacyKey);
        if (legacyValue is not null)
        {
            // Migrate the legacy value to the new key
            var parsed = bool.Parse(legacyValue.Value);
            await _settingsManager.SetAsync(SettingsScope.Global, key, $"{parsed}", actor);
            await _settingsManager.RemoveAsync(SettingsScope.Global, legacyKey, actor);
            return parsed;
        }

        return false;
    }

    async Task SetRunAsync(IRunOnceDataSeeder dataSeeder, User actor)
    {
        var key = GetSeederKey(dataSeeder);
        await _settingsManager.SetBooleanValueAsync(SettingsScope.Global, key, true, actor);
    }

    const string DataSeederPrefix = "DataSeeder:";
    static string GetSeederKey(IRunOnceDataSeeder dataSeeder) =>
        $"{DataSeederPrefix}{dataSeeder.GetType().FullName}|{dataSeeder.Version}";
}

public static partial class DataSeedRunnerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "About to run {SeederCount} {SeederType} data seeders")]
    public static partial void AboutToRunSeeders(this ILogger<DataSeedRunner> logger, int seederCount, string seederType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Running {SeederType}")]
    public static partial void RunningSeeder(this ILogger<DataSeedRunner> logger, Type seederType);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "{SeederType} complete")]
    public static partial void SeederComplete(this ILogger<DataSeedRunner> logger, Type seederType);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Exception while running the data seeder (Type: {SeederType})")]
    public static partial void
        ExceptionRunningDataSeeder(this ILogger logger, Exception exception, Type seederType);
}

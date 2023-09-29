using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class MigratorService : IHostedService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly ILogger<MigratorService> _logger;

    static readonly Histogram<long> MigrationDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "startup.migrations.duration",
        "milliseconds",
        "The duration of the database migration process");

    public MigratorService(IServiceScopeFactory scopeFactory, ILogger<MigratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<Migrator>();
        _logger.ApplyingDatabaseMigrations();

        using var _ = MigrationDuration.Time();
        await migrator.ApplyMigrationsAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static partial class MigratorServiceLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Applying database migrations...")]
    public static partial void ApplyingDatabaseMigrations(this ILogger<MigratorService> logger);
}

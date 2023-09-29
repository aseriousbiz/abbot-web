using System.Diagnostics;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Resources;
using Serious.Abbot;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Web;
using Serious.Logging;

namespace Serious;

public static class Program
{
    static DateTime? _startTime;

    public static DateTime StartTime => _startTime ?? DateTime.UtcNow;

    public static Assembly Assembly => typeof(Program).Assembly;

    public static readonly AssemblyBuildMetadata BuildMetadata = Assembly.GetBuildMetadata();

    public static async Task Main(string[] args)
    {
        // We have to disable the fix for https://github.com/dotnet/efcore/issues/27102
        // It breaks certain complex PostgreSQL queries.
        // See https://github.com/dotnet/efcore/issues/27266
        AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue27102", true);

        _startTime = DateTime.UtcNow;

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();

        using var host = CreateWebHostBuilder(args).Build();

        // Create a logging scope for the entire application, including some version metadata
        // We do this one time, so it's not worth using LoggerMessage.DefineScope, hence we suppress CA1848
        ApplicationLoggerFactory.Configure(host.Services.GetRequiredService<ILoggerFactory>());
        var log = ApplicationLoggerFactory.CreateLogger(typeof(Program));
#pragma warning disable CA1848
        using var loggerScope = log.BeginScope("Commit: {GitCommitId}", BuildMetadata.CommitId);
#pragma warning restore CA1848
        log.Startup();

        await host.RunAsync();
    }

    static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .ConfigureLogging((hostContext, builder) => {
                // Logging is configured in the "outer" host container, so we configure OTEL and Azure Monitor here as well as in Startup.cs
                builder.AddOpenTelemetry(options => {
                    var resourceBuilder = ResourceBuilder.CreateEmpty();
                    AddAbbotOtelService(resourceBuilder);
                    options.SetResourceBuilder(resourceBuilder);
                    if (hostContext.Configuration["ApplicationInsights:ConnectionString"] is
                        { Length: > 0 } appInsightsConnectionString)
                    {
                        options.AddAzureMonitorLogExporter(o => { o.ConnectionString = appInsightsConnectionString; });
                    }
                });
            })
            .ConfigureAppConfiguration((_, builder) => {
                builder.AddFeatureFlagConfiguration();
            })
            .UseStartup<Startup>();

    public static void AddAbbotOtelService(ResourceBuilder resourceBuilder)
    {
        resourceBuilder.AddService(
            serviceName: "Abbot.Web",
            serviceNamespace: "Serious.Abbot",
            serviceVersion: BuildMetadata.CommitId,
            serviceInstanceId: GetServiceInstanceId());
    }

    public static string GetServiceInstanceId() =>
        Environment.GetEnvironmentVariable("ABBOT_INSTANCE_ID") ??
        Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ??
        Environment.MachineName;
}

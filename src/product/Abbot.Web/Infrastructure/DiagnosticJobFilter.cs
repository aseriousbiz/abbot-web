using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Activity = System.Diagnostics.Activity;

#pragma warning disable CA1816
#pragma warning disable CA1063

namespace Serious.Abbot.Infrastructure;

public class DiagnosticJobFilter : IServerFilter
{
    static readonly ILogger<DiagnosticJobFilter> Log = ApplicationLoggerFactory.CreateLogger<DiagnosticJobFilter>();
    const string ActivityName = $"{nameof(DiagnosticJobFilter)}.{nameof(ActivityName)}";
    const string ScopeName = $"{nameof(DiagnosticJobFilter)}.{nameof(ScopeName)}";
    const string TimestampName = $"{nameof(DiagnosticJobFilter)}.{nameof(TimestampName)}";

    static readonly Counter<long> JobCount = AbbotTelemetry.Meter.CreateCounter<long>(
        "jobs.invocation.count",
        "invocations",
        "The number of invocations of a job.");
    static readonly Histogram<long> JobDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "jobs.invocation.duration",
        "invocations",
        "The number of invocations of a job.");

    public void OnPerforming(PerformingContext filterContext)
    {
        var activity =
            new Activity(
                $"{filterContext.BackgroundJob.Job.Type.FullName}.{filterContext.BackgroundJob.Job.Method.Name}");

        activity.Start();

        var scope = Log.BeginPerformJob(
            filterContext.BackgroundJob.Id,
            filterContext.BackgroundJob.Job.Type.Assembly.FullName ?? string.Empty,
            filterContext.BackgroundJob.Job.Type.FullName ?? string.Empty,
            filterContext.BackgroundJob.Job.Method.Name);

        filterContext.Items[ActivityName] = activity;
        filterContext.Items[ScopeName] = scope;
        filterContext.Items[TimestampName] = Stopwatch.GetTimestamp();
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        var metricTag = new TagList()
        {
            { "job_type", filterContext.BackgroundJob.Job.Type.FullName },
            { "retry_count", filterContext.GetJobParameter<int>("RetryCount") },
        };

        if (filterContext.Canceled)
        {
            metricTag.SetCanceled();
        }
        else if (filterContext.Exception is not null)
        {
            // Just make sure we have a clear exception message if a job fails.
            Log.JobException(filterContext.Exception, filterContext.Exception.GetType().FullName, filterContext.Exception.Message, filterContext.ExceptionHandled);
            metricTag.SetFailure(filterContext.Exception);
        }
        else
        {
            metricTag.SetSuccess();
        }

        JobCount.Add(1, metricTag);

        if (filterContext.Items.TryGetValue(TimestampName, out var startTicksVal) && startTicksVal is long startTicks)
        {
            var ms = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
            if (filterContext.BackgroundJob.Job.Type == typeof(SlackEventProcessor))
            {
                Log.SlackJobCompleted((int)ms);
            }
            else
            {
                Log.JobCompleted((int)ms);
            }
            JobDuration.Record((long)ms, metricTag);
        }

        if (filterContext.Items.TryGetValue(ActivityName, out var activityVal) && activityVal is Activity activity)
        {
            activity.Dispose();
        }

        if (filterContext.Items.TryGetValue(ScopeName, out var scopeVal) && scopeVal is IDisposable scope)
        {
            scope.Dispose();
        }
    }
}

static partial class DiagnosticJobFilterLoggingExtensions
{
    static readonly Func<ILogger, string, string, string, string, IDisposable?> PerformJobScope =
        LoggerMessage.DefineScope<string, string, string, string>(
            "Performing Job {JobId}, implemented by {JobAssemblyName}/{JobType}.{JobMethod}");

    public static IDisposable? BeginPerformJob(this ILogger<DiagnosticJobFilter> logger, string jobId,
        string assemblyName, string typeName,
        string methodName)
        => PerformJobScope(logger, jobId, assemblyName, typeName, methodName);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Job completed in {ElapsedMilliseconds}ms.")]
    public static partial void JobCompleted(this ILogger<DiagnosticJobFilter> logger, long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Exception of type '{ExceptionType}' was thrown during job execution (Handled: {ExceptionHandled}): {ExceptionMessage}.")]
    public static partial void JobException(this ILogger<DiagnosticJobFilter> logger, Exception ex,
        string? exceptionType, string exceptionMessage, bool exceptionHandled);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Job completed in {ElapsedMilliseconds}ms.")]
    public static partial void SlackJobCompleted(this ILogger<DiagnosticJobFilter> logger, long elapsedMilliseconds);
}

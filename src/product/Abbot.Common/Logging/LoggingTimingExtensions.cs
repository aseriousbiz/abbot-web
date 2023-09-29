using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Serious.Abbot.Infrastructure;

public static class LoggingTimingExtensions
{
    public static async Task LogElapsedAsync(
        this ILogger logger,
        string eventName,
        Func<Task> action)
        => await LogElapsedAsync(logger, eventName, null, action);

    public static async Task LogElapsedAsync(
        this ILogger logger,
        string eventName,
        IReadOnlyDictionary<string, string>? properties,
        Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action();
        }
        finally
        {
            logger.LogTiming(eventName, properties, stopwatch.Elapsed);
        }
    }

    public static async Task<T> LogElapsedAsync<T>(
        this ILogger logger,
        string eventName,
        Func<Task<T>> action)
        => await LogElapsedAsync(logger, eventName, null, action);

    public static async Task<T> LogElapsedAsync<T>(
        this ILogger logger,
        string eventName,
        IReadOnlyDictionary<string, string>? properties,
        Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            logger.LogTiming(eventName, properties, stopwatch.Elapsed);
        }
    }

    public static T LogElapsed<T>(this ILogger logger, string eventName, Func<T> action)
        => LogElapsed(logger, eventName, null, action);

    public static T LogElapsed<T>(this ILogger logger, string eventName, IReadOnlyDictionary<string, string>? properties,
        Func<T> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return action();
        }
        finally
        {
            logger.LogTiming(eventName, properties, stopwatch.Elapsed);
        }
    }

    public static void LogElapsed(this ILogger logger, string eventName, Action action)
        => LogElapsed(logger, eventName, null, action);

    public static void LogElapsed(this ILogger logger, string eventName, IReadOnlyDictionary<string, string>? properties,
        Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            logger.LogTiming(eventName, properties, stopwatch.Elapsed);
        }
    }

    static void LogTiming(this ILogger logger, string eventName, IReadOnlyDictionary<string, string>? properties, TimeSpan elapsed)
    {
        var elapsedText = elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        var props = properties is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(properties);
        props["Elapsed"] = elapsedText;

        logger.Log(LogLevel.Information,
            new EventId(0, eventName),
            props,
            null,
            (_, _) => $"Event {eventName} took {elapsedText} ms to complete.");
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Telemetry;

public static class AbbotTelemetry
{
#pragma warning disable CA1802
    static readonly string SourceName = "Serious.Abbot";
#pragma warning restore CA1802
    public static readonly ActivitySource ActivitySource = new(SourceName, typeof(AbbotTelemetry).Assembly.GetBuildMetadata().CommitId);
    public static readonly Meter Meter = new(SourceName, typeof(AbbotTelemetry).Assembly.GetBuildMetadata().CommitId);

    public static TagList CreateOrganizationTags(Organization organization)
    {
        return new()
        {
            // WARNING: When we have a lot of organizations, we might want to replace this tag with one that tracks some less-specific metric like Plan Type.
            { "Organization", organization.PlatformId }
        };
    }
}

public static class MetricExtensions
{
    /// <summary>
    /// Starts a timer that will emit the elapsed milliseconds to the provided <paramref name="histogram"/> when disposed.
    /// </summary>
    /// <param name="histogram">The <see cref="Histogram{long}"/> to emit the elapsed milliseconds to.</param>
    /// <returns>A <see cref="TimeDisposable"/> that will stop timing and emit the elapsed milliseconds when disposed.</returns>
    public static TimeDisposable Time(this Histogram<long> histogram) =>
        new(Stopwatch.GetTimestamp(), histogram, new());

    /// <inheritdoc cref="Time(Histogram{long})"/>
    /// <param name="tags">A <see cref="TagList"/> containing tags to apply to the metric when emitted.</param>
    public static TimeDisposable Time(this Histogram<long> histogram, in TagList tags) =>
        new(Stopwatch.GetTimestamp(), histogram, tags);

    /// <summary>
    /// Starts a timer that will emit the elapsed milliseconds to the provided <paramref name="histogram"/>.
    /// </summary>
    /// <param name="histogram">The <see cref="Histogram{long}"/> to emit the elapsed milliseconds to.</param>
    /// <param name="selector">The function to time.</param>
    /// <returns>Result of <paramref name="selector"/>.</returns>
    public static TResult Time<TResult>(this Histogram<long> histogram, Func<TResult> selector)
    {
        using var _ = histogram.Time();
        return selector();
    }

    /// <inheritdoc cref="Time{TResult}(Histogram{long}, Func{TResult})"/>
    /// <param name="tags">A <see cref="TagList"/> containing tags to apply to the metric when emitted.</param>
    public static TResult Time<TResult>(this Histogram<long> histogram, in TagList tags, Func<TResult> selector)
    {
        using var _ = histogram.Time(tags);
        return selector();
    }

    /// <summary>
    /// Starts a timer that will emit the elapsed milliseconds to the provided <paramref name="histogram"/>.
    /// </summary>
    /// <param name="histogram">The <see cref="Histogram{long}"/> to emit the elapsed milliseconds to.</param>
    /// <param name="selector">The function to time.</param>
    /// <returns>Awaited <see cref="Task{TResult}"/> from <paramref name="selector"/>.</returns>
    public static async Task<TResult> Time<TResult>(this Histogram<long> histogram, Func<Task<TResult>> selector)
    {
        using var _ = histogram.Time();
        return await selector();
    }

    /// <inheritdoc cref="Time{TResult}(Histogram{long}, Func{Task{TResult}})"/>
    /// <param name="tags">A <see cref="TagList"/> containing tags to apply to the metric when emitted.</param>
    public static async Task<TResult> Time<TResult>(this Histogram<long> histogram, TagList tags, Func<Task<TResult>> selector)
    {
        using var _ = histogram.Time(tags);
        return await selector();
    }

    public readonly record struct TimeDisposable(long StartTimestamp, Histogram<long> Histogram, in TagList Tags) : IDisposable
    {
        public void Dispose()
        {
            // SO GLAD they finally made a helper for this instead of making me do MATH 🤮.
            var elapsed = Stopwatch.GetElapsedTime(StartTimestamp);
            Histogram.Record((long)elapsed.TotalMilliseconds, Tags);
        }
    }
}

public static class MetricTags
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TagList SetSuccess(this ref TagList tags)
    {
        tags.Add("outcome", "Success");
        return ref tags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TagList SetCanceled(this ref TagList tags)
    {
        tags.Add("outcome", "Canceled");
        return ref tags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TagList SetSkipped(this ref TagList tags)
    {
        tags.Add("outcome", "Skipped");
        return ref tags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TagList SetFailure(this ref TagList tags, string? error)
    {
        tags.Add("outcome", "Failure");
        tags.Add("error", error ?? "Unknown");
        return ref tags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TagList SetFailure(this ref TagList tags, Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            tags = SetCanceled(ref tags);
        }
        else
        {
            tags.Add("outcome", "Failure");
        }

        tags.Add("error", ex.GetType().Name);
        return ref tags;
    }
}

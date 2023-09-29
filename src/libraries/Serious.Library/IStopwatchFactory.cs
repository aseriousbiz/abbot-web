using System;
using System.Diagnostics;

namespace Serious;

public interface IStopwatch
{
    /// <summary>
    /// Returns the elapsed time.
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Returns the elapsed time in milliseconds.
    /// </summary>
    long ElapsedMilliseconds { get; }
}

/// <summary>
/// An interface for a stopwatch.
/// </summary>
public interface IStopwatchFactory
{
    /// <summary>
    /// Creates a new <see cref="IStopwatchFactory"/> and starts it.
    /// </summary>
    /// <returns>A <see cref="IStopwatchFactory"/>.</returns>
    public IStopwatch StartNew();
}

public class SystemStopwatch : IStopwatch
{
    readonly Stopwatch _stopwatch;

    public SystemStopwatch(Stopwatch stopwatch)
    {
        _stopwatch = stopwatch;
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
}

public class SystemStopwatchFactory : IStopwatchFactory
{
    public IStopwatch StartNew()
    {
        return new SystemStopwatch(Stopwatch.StartNew());
    }
}

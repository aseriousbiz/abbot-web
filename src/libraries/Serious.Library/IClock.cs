using System;

namespace Serious;

/// <summary>
/// Abstracts over a clock, which provides the current wall clock time.
/// Wall clock time is **non-monotonic** which means there is no guarantee that subsequent calls to
/// <see cref="IClock.UtcNow"/> will return monotonically increasing values.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets an <see cref="IClock"/> representing the system clock.
    /// </summary>
    static readonly IClock System = new SystemClock();

    /// <summary>
    /// Gets the current time according to the clock.
    /// </summary>
    DateTime UtcNow { get; }
}

/// <summary>
/// An <see cref="IClock"/> backed by the system clock.
/// </summary>
public class SystemClock : IClock
{
    /// <summary>
    /// Gets the current system wall clock time, according to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// An <see cref="IClock"/> that can be frozen to a specific time, shifted to a new time, or restored to the current system clock time.
/// </summary>
public class TimeTravelClock : IClock
{
    DateTime? _overrideTime;

    /// <summary>
    /// Gets the current time according to the clock.
    /// If the clock is frozen, this will return the frozen time.
    /// If the clock is not frozen, this will return the current system wall clock time.
    /// </summary>
    public DateTime UtcNow => _overrideTime ?? DateTime.UtcNow;

    /// <summary>
    /// Gets a boolean indicating if this clock is currently frozen.
    /// </summary>
    public bool IsFrozen => _overrideTime is not null;

    /// <summary>
    /// Creates a new <see cref="TimeTravelClock"/> that is not frozen and reports system wall clock time.
    /// After construction, the clock can be frozen using any of the time travel methods on <see cref="TimeTravelClock"/>.
    /// </summary>
    public TimeTravelClock()
    {
    }

    /// <summary>
    /// Creates a new <see cref="TimeTravelClock"/> that is frozen at the specified time.
    /// </summary>
    /// <param name="timeUtc">The time at which to freeze the clock. Regardless of the value of <see cref="DateTime.Kind"/>, this value is assumed to be UTC.</param>
    public TimeTravelClock(DateTime timeUtc)
    {
        _overrideTime = timeUtc;
    }

    /// <summary>
    /// Returns this clock to the current system wall clock time and unfreezes it.
    /// </summary>
    public void ReturnToNormalTime() => _overrideTime = null;

    /// <summary>
    /// Freezes this clock at the current system wall clock time.
    /// </summary>
    public DateTime Freeze()
    {
        _overrideTime = UtcNow;
        return _overrideTime.Value;
    }

    /// <summary>
    /// Freezes this clock at the specified time.
    /// </summary>
    /// <param name="timeUtc">The time at which to freeze the clock. Regardless of the value of <see cref="DateTime.Kind"/>, this value is assumed to be UTC.</param>
    public DateTime TravelTo(DateTime timeUtc)
    {
        _overrideTime = DateTime.SpecifyKind(timeUtc, DateTimeKind.Utc);
        return _overrideTime.Value;
    }

    /// <summary>
    /// Freezes this clock at a time that is <paramref name="time"/> ahead of the current value of <see cref="UtcNow"/>.
    /// If the clock is not currently frozen, it will be frozen by this call.
    /// </summary>
    /// <param name="time">The interval to advance the value of <see cref="UtcNow"/> by.</param>
    public DateTime AdvanceBy(TimeSpan time)
    {
        _overrideTime = UtcNow + time;
        return _overrideTime.Value;
    }
}

using System;
using System.Collections.Generic;

namespace Serious;

public static class DurationFormat
{
    static readonly Dictionary<string, TimeUnit> UnitNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "w", TimeUnit.Weeks },
        { "wk", TimeUnit.Weeks },
        { "wks", TimeUnit.Weeks },
        { "week", TimeUnit.Weeks },
        { "weeks", TimeUnit.Weeks },
        { "d", TimeUnit.Days },
        { "day", TimeUnit.Days },
        { "days", TimeUnit.Days },
        { "h", TimeUnit.Hours },
        { "hr", TimeUnit.Hours },
        { "hrs", TimeUnit.Hours },
        { "hour", TimeUnit.Hours },
        { "hours", TimeUnit.Hours },
        { "m", TimeUnit.Minutes },
        { "min", TimeUnit.Minutes },
        { "mins", TimeUnit.Minutes },
        { "minute", TimeUnit.Minutes },
        { "minutes", TimeUnit.Minutes },
        { "s", TimeUnit.Seconds },
        { "sec", TimeUnit.Seconds },
        { "secs", TimeUnit.Seconds },
        { "second", TimeUnit.Seconds },
        { "seconds", TimeUnit.Seconds },
    };

    enum TimeUnit
    {
        None,
        Weeks,
        Days,
        Hours,
        Minutes,
        Seconds
    }

    /// <summary>
    /// Parses a duration in Abbot's default format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Abbot's default duration format is inspired by Go's duration format (https://pkg.go.dev/time#ParseDuration)
    /// but with some modifications to make it as user-friendly as possible.
    /// </para>
    /// <para>
    /// A duration is a sequence of magnitude-unit pairs which will be combined to form the full duration.
    /// The duration can contain "list" punctuation (',' and ';') and whitespace before or after any magnitude or unit.
    /// The 'magnitude' comes first, and is an unsigned floating point number (scientific notation is not supported).
    /// After the magnitude, a unit must be provided (see a full list of units below).
    /// Units can come in any order but cannot be specified more than once.
    /// </para>
    /// <para>
    /// Allowed Units (all units are case-insensitive):
    ///
    /// * 'Weeks' (can also be specified as 'Week', 'Wks', 'Wk', or 'W')
    /// * 'Days' (can also be specified as: 'Day', or 'D')
    /// * 'Hours' (can also be specified as: 'Hour', 'Hrs', 'Hr', or 'H')
    /// * 'Minutes' (can also be specified as: 'Minute', 'Mins', 'Min', or 'M')
    /// * 'Seconds' (can also be specified as: 'Second', 'Secs', 'Sec', or 'S')
    /// </para>
    /// </remarks>
    /// <example>
    /// The following are valid durations:
    ///
    /// * 1d ==> 1 Day
    /// * 24hr ==> 1 Day
    /// * 1d, 2h ==> 1 Day, 2 Hours (26 total hours)
    /// * 1d; 2h; 30m ==> 1 Day, 2 Hours, 30 Minutes (26.5 total hours)
    /// * 30 minutes 1 day  ==> 1 Day, 30 Minutes (24.5 total hours)
    /// </example>
    /// <param name="duration">The duration string to parse.</param>
    /// <param name="result">A <see cref="TimeSpan"/> representing the duration, if this method returns <c>true</c></param>
    /// <returns>Returns <c>true</c> if parsing was successful.</returns>
    public static bool TryParse(string duration, out TimeSpan result)
    {
        var cursor = 0;
        result = TimeSpan.Zero;
        var processedUnits = new HashSet<TimeUnit>();
        SkipSpacers(duration, ref cursor);
        while (cursor < duration.Length)
        {
            if (!TryParseMagnitude(duration, ref cursor, out var magnitude) ||
                !TryParseUnits(duration, ref cursor, out var units))
            {
                result = default;
                return false;
            }

            if (!processedUnits.Add(units))
            {
                // Already seen this unit
                result = default;
                return false;
            }

            switch (units)
            {
                case TimeUnit.Weeks:
                    result += TimeSpan.FromDays(magnitude * 7);
                    break;
                case TimeUnit.Days:
                    result += TimeSpan.FromDays(magnitude);
                    break;
                case TimeUnit.Hours:
                    result += TimeSpan.FromHours(magnitude);
                    break;
                case TimeUnit.Minutes:
                    result += TimeSpan.FromMinutes(magnitude);
                    break;
                case TimeUnit.Seconds:
                    result += TimeSpan.FromSeconds(magnitude);
                    break;
            }

            SkipSpacers(duration, ref cursor);
        }

        return true;
    }

    static bool TryParseUnits(string duration, ref int cursor, out TimeUnit unit)
    {
        SkipSpacers(duration, ref cursor);

        var start = cursor;
        for (; cursor < duration.Length && char.IsLetter(duration[cursor]); cursor++)
        {
        }

        if (cursor > start)
        {
            var s = duration[start..cursor];
            return UnitNames.TryGetValue(s, out unit);
        }
        else
        {
            unit = TimeUnit.None;
            return false;
        }
    }

    static bool TryParseMagnitude(string duration, ref int cursor, out double magnitude)
    {
        var start = cursor;
        for (; cursor < duration.Length && (duration[cursor] == '.' || char.IsDigit(duration[cursor])); cursor++)
        {
        }

        if (cursor > start)
        {
            var s = duration[start..cursor];
            return double.TryParse(s, out magnitude);
        }
        else
        {
            // Empty "magnitude" is valid, but we return zero.
            magnitude = 0;
            return false;
        }
    }

    static void SkipSpacers(string duration, ref int cursor)
    {
        // Skip whitespace and "spacing" punctuation like ',', ';', etc.
        for (;
             cursor < duration.Length && (duration[cursor] == ',' || duration[cursor] == ';'
                                                                  || char.IsWhiteSpace(duration[cursor]));
             cursor++)
        {
        }
    }
}

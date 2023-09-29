using System.Text.RegularExpressions;
using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Extensions to <see cref="IArgument"/> to make it easier to work with arguments.
/// </summary>
public static class ArgumentExtensions
{
    static readonly Regex TimeRegex = new(
        @"^(?<hr>\d\d?)(?:\:(?<min>\d\d)(?:\:(?<sec>\d\d))?)?\s*(?<period>[ap]m)|(?<hr>\d\d?)\:?(?<min>\d\d)(?:\:(?<sec>\d\d))?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses the argument value and returns it as a <see href="https://nodatime.org/2.2.x/api/NodaTime.LocalTime.html">LocalTime</see> if it matches the format, otherwise
    /// returns null.
    /// </summary>
    /// <param name="argument">The argument.</param>
    public static LocalTime? ToLocalTime(this IArgument argument)
    {
        var match = TimeRegex.Match(argument.Value);
        if (match.Success)
        {
            var hour = int.TryParse(match.Groups["hr"].Value, out var hr)
                ? hr
                : -1;
            var minutes = int.TryParse(match.Groups["min"].Value ?? "0", out var mins)
                ? mins
                : 0;
            var seconds = int.TryParse(match.Groups["sec"].Value ?? "0", out var sec)
                ? sec
                : 0;
            var period = match.Groups["period"].Value;

            if (hour is >= 0 and < 24
                && minutes is >= 0 and <= 59
                && (period is { Length: > 1 } && hour < 13 || period is null or { Length: 0 }))
            {
                if (period is "pm")
                {
                    hour += 12;
                }

                return new LocalTime(hour, mins, seconds);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts this local time into a zoned date time for the target time zone. It assumes today as the starting
    /// point. If the local time is before today, then it returns the time the next day.
    /// </summary>
    /// <param name="localTime">The local time.</param>
    /// <param name="source">The source time zone the local time is assumed to be in.</param>
    /// <param name="target">The target time zone.</param>
    public static ZonedDateTime ToTimeZone(
        this LocalTime localTime,
        DateTimeZone source,
        DateTimeZone target) => ToTimeZone(localTime, source, target, SystemClock.Instance.GetCurrentInstant());

    /// <summary>
    /// Converts this local time into a zoned date time for the target time zone. It assumes today as the starting
    /// point. If the local time is before today, then it returns the time the next day.
    /// </summary>
    /// <param name="localTime">The local time.</param>
    /// <param name="source">The source time zone the local time is assumed to be in.</param>
    /// <param name="target">The target time zone.</param>
    /// <param name="now">The current instance to use.</param>
    public static ZonedDateTime ToTimeZone(
        this LocalTime localTime,
        DateTimeZone source,
        DateTimeZone target,
        Instant now)
    {
        var sourceDate = now.InZone(source).Date.At(localTime);
        var inZoneDate = source.AtLeniently(sourceDate);

        if (inZoneDate.ToInstant() < now)
        {
            // Since the time is in the past, we want to know what the local time is tomorrow in the target timezone.
            var tomorrow = now.Plus(Duration.FromDays(1)).InZone(source).Date.At(localTime);
            inZoneDate = source.AtLeniently(tomorrow);
        }

        var sourceInstant = inZoneDate.ToInstant();
        return sourceInstant.InZone(target);
    }

    /// <summary>
    /// Parses the argument value as an Int32 and returns the value or null if it is not an integer. Supports
    /// numbers in the format #123 where it returns 123.
    /// </summary>
    /// <param name="argument">The argument.</param>
    public static int? ToInt32(this IArgument argument)
    {
        var value = argument.Value;
        if (value is { Length: 0 })
        {
            return null;
        }

        if (value is { Length: > 1 } && value[0] == '#')
        {
            value = value[1..];
        }

        return int.TryParse(value, out var result)
            ? result
            : null;
    }
}

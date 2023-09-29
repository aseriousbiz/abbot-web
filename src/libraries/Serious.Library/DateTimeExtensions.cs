using NodaTime;
using NodaTime.Extensions;

namespace System;

/// <summary>
/// Extensions to <see cref="DateTime"/> that are generally useful.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Returns the local date and time in the specified time zone for the UTC date.
    /// </summary>
    /// <param name="utcDate">The utc <see cref="DateTime"/>.</param>
    /// <param name="tz">The IANA timezone id.</param>
    public static DateTime ToDateTimeInTimeZone(this DateTime utcDate, DateTimeZone tz)
    {
        return tz == DateTimeZone.Utc
            ? utcDate
            : utcDate.ToZonedDateTime(tz).LocalDateTime.ToDateTimeUnspecified();
    }

    /// <summary>
    /// Returns the local date and time in the specified time zone for the UTC date.
    /// </summary>
    /// <param name="utcDate">The utc <see cref="DateTime"/>.</param>
    /// <param name="tz">The IANA timezone id.</param>
    public static LocalDateTime ToLocalDateTimeInTimeZone(this DateTime utcDate, string tz)
    {
        return utcDate.ToZonedDateTime(tz).LocalDateTime;
    }

    /// <summary>
    /// Returns the local date and time in the specified time zone for the UTC date.
    /// </summary>
    /// <param name="utcDate">The utc <see cref="DateTime"/>.</param>
    /// <param name="timezone">The timezone.</param>
    /// <returns>The utc date a <see cref="LocalDateTime"/> in the specified timezone.</returns>
    public static LocalDateTime ToLocalDateTimeInTimeZone(this DateTime utcDate, DateTimeZone timezone)
    {
        return utcDate.ToZonedDateTime(timezone).LocalDateTime;
    }

    /// <summary>
    /// Returns the date and time in the specified time zone.
    /// </summary>
    /// <param name="utcDate">The utc <see cref="DateTime"/>.</param>
    /// <param name="tz">The IANA timezone id.</param>
    /// <returns>The utc date as a <see cref="ZonedDateTime"/> in the specified timezone.</returns>
    public static ZonedDateTime ToZonedDateTime(this DateTime utcDate, string tz)
    {
        var timezone = DateTimeZoneProviders.Tzdb[tz];
        return utcDate.ToZonedDateTime(timezone);
    }

    /// <summary>
    /// Returns the date and time in the specified time zone.
    /// </summary>
    /// <param name="utcDate">The utc <see cref="DateTime"/>.</param>
    /// <param name="timezone">The timezone.</param>
    /// <returns>The utc date as a <see cref="ZonedDateTime"/> in the specified timezone.</returns>
    public static ZonedDateTime ToZonedDateTime(this DateTime utcDate, DateTimeZone timezone)
    {
        var instant = Instant.FromDateTimeUtc(utcDate);
        return instant.InZone(timezone);
    }

    /// <summary>
    /// Converts a given <see cref="DateOnly"/> and <see cref="TimeOnly"/>, assumes they are in the specified
    /// <see cref="DateTimeZone"/>, and converts them to a UTC <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timezone">The timezone the date and time are in.</param>
    /// <param name="dateOnly">The date.</param>
    /// <param name="timeOnly">The time.</param>
    /// <returns>A UTC DateTime.</returns>
    public static DateTime ToUtcDateTime(this DateOnly dateOnly, TimeOnly timeOnly, DateTimeZone timezone)
    {
        var localDateTime = dateOnly.ToLocalDate() + timeOnly.ToLocalTime();
        return localDateTime.ToUtcDateTime(timezone);
    }

    /// <summary>
    /// Converts a given <see cref="LocalDateTime"/> in the specified <see cref="DateTimeZone"/>, and converts that
    /// date a UTC <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timezone">The timezone the date and time are in.</param>
    /// <param name="localDateTime">The date.</param>
    /// <returns>A UTC DateTime.</returns>
    public static DateTime ToUtcDateTime(this LocalDateTime localDateTime, DateTimeZone timezone)
    {
        var zonedDateTime = localDateTime.InZoneLeniently(timezone);
        return zonedDateTime.ToInstant().ToDateTimeUtc();
    }

    /// <summary>
    /// Returns a date formatted as a string that MixPanel expects. This is the same as the ISO 8601 format, but
    /// without the fractional seconds and "Z" at the end, at least according to the
    /// <see href="https://help.mixpanel.com/hc/en-us/articles/115004547063-Properties-Supported-Data-Types#date">examples in the docs</see>.
    /// </summary>
    /// <remarks>
    /// Use this when we have to render a date to the client that will be picked up by JavaScript.
    /// </remarks>
    /// <param name="dateTime">The UTC date time.</param>
    /// <returns></returns>
    public static string ToMixPanelDateString(this DateTime dateTime)
    {
        return dateTime.ToString("s");
    }
}

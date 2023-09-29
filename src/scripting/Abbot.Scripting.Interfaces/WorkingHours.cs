using System;
using NodaTime;
using NodaTime.Extensions;

namespace Serious.Abbot.Scripting;

/// <summary>
/// A set of working hours.
/// </summary>
/// <param name="Start">The start time.</param>
/// <param name="End">The end time.</param>
public record WorkingHours(TimeOnly Start, TimeOnly End)
{
    /// <summary>
    /// The default set of working hours, aka 9am to 5pm. What a way to make a living!
    /// </summary>
    public static readonly WorkingHours Default = new(new(9, 0), new(17, 0));

    /// <summary>
    /// The duration of the working hours.
    /// </summary>
    /// <remarks>
    /// You might think you could just subtract Start from End, but you'd be wrong! Consider a graveyard shift worker
    /// who starts in the evening and finishes the following morning.
    /// </remarks>
    public TimeSpan Duration => End > Start
        ? End - Start
        : TimeSpan.FromDays(1) - (Start - End);

    /// <summary>
    /// Checks if the given time is within the working hours.
    /// </summary>
    /// <remarks>
    /// You might think that this is a simple "time is between Start and End" check, but you'd be wrong!
    /// Consider a graveyard shift worker who starts in the evening and finishes the following morning.
    /// The basic rule here is that "working hours" go from Start to End, regardless of the current Date.
    /// </remarks>
    /// <param name="time">The time to check against the working hours.</param>
    /// <returns>Returns <c>true</c> if the provided time is within working hours.</returns>
    public bool Contains(TimeOnly time) => End > Start
        ? time >= Start && time < End
        : time >= Start || time < End;

    /// <summary>
    /// Checks if the given time is within the working hours.
    /// </summary>
    /// <remarks>
    /// You might think that this is a simple "time is between Start and End" check, but you'd be wrong!
    /// Consider a graveyard shift worker who starts in the evening and finishes the following morning.
    /// The basic rule here is that "working hours" go from Start to End, regardless of the current Date.
    /// </remarks>
    /// <param name="time">The time to check against the working hours.</param>
    /// <returns>Returns <c>true</c> if the provided time is within working hours.</returns>
    public bool Contains(LocalTime time) => Contains(time.ToTimeOnly());

    /// <summary>
    /// Checks if the time represented by the given UTC date is is within the working hours for the specified
    /// timezone.
    /// </summary>
    /// <param name="utcDate">The UTC date.</param>
    /// <param name="timezoneId">An IANA timezone id.</param>
    public bool Contains(DateTime utcDate, string timezoneId)
    {
        var zonedDateTime = GetZonedDateTime(utcDate, timezoneId);
        var timeOnly = zonedDateTime.TimeOfDay;

        return Contains(timeOnly);
    }

    /// <summary>
    /// Given a DateTime and a timezone, returns the DateTime of the start of the next working hours.
    /// </summary>
    /// <param name="utcDate">The UTC date.</param>
    /// <param name="timezoneId">An IANA timezone id.</param>
    public DateTime GetNextDateWithinWorkingHours(DateTime utcDate, string timezoneId)
    {
        var zonedDateTime = GetZonedDateTime(utcDate, timezoneId);
        var timeOnly = zonedDateTime.TimeOfDay;

        var nextWorkingHoursStart = timeOnly.ToTimeOnly() < Start
            ? zonedDateTime.Date.At(Start.ToLocalTime())
            : zonedDateTime.Date.PlusDays(1).At(Start.ToLocalTime());

        // Convert it back to UTC
        var sourceTimeZone = DateTimeZoneProviders.Tzdb[timezoneId];
        var targetTimeZone = DateTimeZoneProviders.Tzdb["UTC"];
        var sourceZonedDateTime = nextWorkingHoursStart.InZoneLeniently(sourceTimeZone);
        ZonedDateTime targetZonedDateTime = sourceZonedDateTime.WithZone(targetTimeZone);
        return targetZonedDateTime.ToDateTimeUtc();
    }

    static ZonedDateTime GetZonedDateTime(DateTime utcDate, string timezoneId)
    {
        var tz = DateTimeZoneProviders.Tzdb[timezoneId];
        var instant = utcDate.ToUniversalTime().ToInstant();
        var zonedDateTime = instant.InZone(tz);
        return zonedDateTime;
    }

    /// <summary>
    /// Returns a human-readable string representation of the working hours.
    /// </summary>
    public string Humanize()
    {
        // We explicitly want lower case here.
        return $"{Start:h:mmtt}-{End:h:mmtt}".ToLowerInvariant();
    }
}

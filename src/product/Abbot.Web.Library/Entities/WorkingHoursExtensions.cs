using System.Collections.Generic;
using System.Linq;
using NodaTime;

namespace Serious.Abbot.Entities;

public static class WorkingHoursExtensions
{
    /// <summary>
    /// Takes a collection of <see cref="WorkingHours"/> and collapses them into the smallest set of
    /// working hours that span all the provided working hours.
    /// </summary>
    /// <remarks>
    /// As a first simple example, consider the following working hours:
    /// * 01:00 - 03:00
    /// * 02:00 - 04:00
    /// * 03:00 - 05:00
    ///
    /// These would collapse into a single range: 01:00 - 05:00.
    ///
    /// However, consider an example where there are non-overlapping ranges:
    ///
    /// * 01:00 - 03:00
    /// * 02:00 - 04:00
    /// * 06:00 - 08:00
    /// * 07:00 - 09:00
    ///
    /// This would collapse into two ranges:
    ///
    /// * 01:00 - 04:00
    /// * 06:00 - 09:00
    /// </remarks>
    /// <param name="hours">The set of <see cref="WorkingHours"/> to collapse</param>
    /// <returns>A new set of <see cref="WorkingHours"/> that represent the collapsed set of working hours.</returns>
    public static IEnumerable<WorkingHours> Collapse(this IEnumerable<WorkingHours> hours)
    {
        TimeOnly? currentStart = null;
        for (int i = 0; i < 48; i++)
        {
            var candidate = new TimeOnly(i / 2, i % 2 == 0 ? 0 : 30);
            if (hours.Any(h => h.Contains(candidate)))
            {
                currentStart ??= candidate;
            }
            else
            {
                if (currentStart is not null)
                {
                    yield return new(currentStart.Value, candidate);
                    currentStart = null;
                }
            }
        }

        if (currentStart is not null)
        {
            yield return new(currentStart.Value, TimeOnly.MinValue);
        }
    }

    /// <summary>
    /// Converts the <see cref="WorkingHours"/> from <paramref name="sourceTimeZone"/> to <paramref name="targetTimeZone"/>.
    /// </summary>
    /// <param name="workingHours">The working hours to change the timezone for.</param>
    /// <param name="nowUtc">The current date, required for handling daylight savings time.</param>
    /// <param name="sourceTimeZone">The time zone to convert from.</param>
    /// <param name="targetTimeZone">The time zone to convert to.</param>
    /// <returns>A new <see cref="WorkingHours"/> instance in the target time zone</returns>
    public static WorkingHours ChangeTimeZone(
        this WorkingHours workingHours,
        DateTime nowUtc,
        DateTimeZone sourceTimeZone,
        DateTimeZone targetTimeZone)
    {
        if (nowUtc.Kind is not DateTimeKind.Utc)
        {
            throw new ArgumentException("nowUtc must be in UTC", nameof(nowUtc));
        }

        var nowSource = nowUtc.ToZonedDateTime(sourceTimeZone);

        var startTime = LocalTime.FromTicksSinceMidnight(workingHours.Start.Ticks);
        var endTime = LocalTime.FromTicksSinceMidnight(workingHours.End.Ticks);

        var startDateTime = nowSource.Date.At(startTime).InZoneLeniently(sourceTimeZone);
        var endDateTime = nowSource.Date.At(endTime).InZoneLeniently(sourceTimeZone);

        var targetStart = startDateTime.WithZone(targetTimeZone);
        var targetEnd = endDateTime.WithZone(targetTimeZone);

        return new(new(targetStart.TimeOfDay.TickOfDay), new(targetEnd.TimeOfDay.TickOfDay));
    }

    /// <summary>
    /// Calculates coverage for a set of workers in a given time zone. If a worker's timezone is not known, they
    /// are excluded from the calculation.
    /// </summary>
    /// <param name="workers">The set of workers.</param>
    /// <param name="targetTimeZone">The TimeZone to calculate coverage for.</param>
    /// <param name="defaultWorkingHours">The default working hours.</param>
    /// <param name="nowUtc">The current date and time in UTC.</param>
    public static IEnumerable<WorkingHours> CalculateCoverage(
        this IEnumerable<IWorker> workers,
        DateTimeZone targetTimeZone,
        WorkingHours defaultWorkingHours,
        DateTime nowUtc)
    {
        return workers
            .Where(w => w.TimeZone is not null)
            .Select(w => (w.WorkingHours ?? defaultWorkingHours).ChangeTimeZone(
                nowUtc,
                w.TimeZone!,
                targetTimeZone))
            .Collapse();
    }

    /// <summary>
    /// Given a set of workers
    /// </summary>
    /// <param name="workers">The set of workers.</param>
    /// <param name="defaultWorkingHours">The default working hours.</param>
    /// <param name="startDateUtc"></param>
    /// <param name="responseDateUtc"></param>
    /// <returns></returns>
    public static TimeSpan CalculateResponseTimeWorkingHours(
        this IEnumerable<IWorker> workers,
        WorkingHours defaultWorkingHours,
        DateTime startDateUtc,
        DateTime responseDateUtc)
        => CalculateCoverage(workers, DateTimeZone.Utc, defaultWorkingHours, startDateUtc)
            .CalculateResponseTimeWorkingHours(DateTimeZone.Utc, startDateUtc, responseDateUtc);

    static TimeSpan CalculateResponseTimeWorkingHours(
        this IEnumerable<WorkingHours> coverage,
        DateTimeZone targetTimeZone,
        DateTime startDateUtc,
        DateTime responseDateUtc)
    {
        var startDate = startDateUtc.ToDateTimeInTimeZone(targetTimeZone);
        var responseDate = responseDateUtc.ToDateTimeInTimeZone(targetTimeZone);

        var responseTimeRange = GetResponseTimeRange(startDate, responseDate);

        return coverage
            .Select(workingHours => GetResponseTimeWorkingHoursInRange(workingHours, responseTimeRange))
            .Sum();
    }

    /// <summary>
    /// Calculates the response time in working hours between the <paramref name="startDate"/> and the
    /// <paramref name="responseDate"/>.
    /// </summary>
    /// <param name="workingHours">The working hours.</param>
    /// <param name="startDate">The local start date.</param>
    /// <param name="responseDate">The local end date.</param>
    public static TimeSpan CalculateResponseTimeWorkingHours(
        this WorkingHours workingHours,
        DateTime startDate,
        DateTime responseDate)
    {
        if (responseDate < startDate)
        {
            throw new ArgumentException("End date must be greater than or equal to start date.");
        }

        var responseTimeRange = GetResponseTimeRange(startDate, responseDate);

        if (workingHours.Start > workingHours.End
            && new[] { workingHours }.Collapse().ToList() is [var morning, var night]) // Graveyard shift
        {
            return GetResponseTimeWorkingHoursInRange(morning, responseTimeRange)
                + GetResponseTimeWorkingHoursInRange(night, responseTimeRange);
        }

        return GetResponseTimeWorkingHoursInRange(workingHours, responseTimeRange);
    }

    static TimeSpan GetResponseTimeWorkingHoursInRange(WorkingHours workingHours, ResponseTimeRange responseTimeRange)
    {
        return ElapsedWorkingTimeSingleDay(workingHours, responseTimeRange.Start)
            + ElapsedWorkingTimeSingleDay(workingHours, responseTimeRange.End)
            + workingHours.Duration * responseTimeRange.DaysBetweenCount;
    }

    static TimeSpan ElapsedWorkingTimeSingleDay(WorkingHours workingHours, ResponseTimeDay? responseTimeDay)
    {
        if (responseTimeDay is null)
        {
            return TimeSpan.Zero;
        }

        var (startTime, endTime) = workingHours;
        var (startDate, responseDate) = responseTimeDay;

        // Convert the working hours to dates for comparison.
        var workingHoursStartDate = startDate.Date.Add(startTime.ToTimeSpan());
        var workingHoursEndDate = startDate.Date.Add(endTime.ToTimeSpan());
        if (workingHoursEndDate == workingHoursStartDate.Date)
        {
            workingHoursEndDate = workingHoursStartDate.Date.AddDays(1).Date;
        }

        // We can't use Contains because it only looks at TimeOnly and doesn't correctly handle midnight the next
        // day.
        bool ContainsDate(DateTime date) => date >= workingHoursStartDate && date < workingHoursEndDate;

        if (responseDate == startDate)
        {
            return TimeSpan.Zero;
        }

        if (workingHoursStartDate > workingHoursEndDate)
        {
            throw new InvalidOperationException("Make sure to call Collapse first so graveyard shifts are split into non-overlapping non-graveyard shifts.");
        }

        if (startDate > workingHoursEndDate || responseDate < workingHoursStartDate)
        {
            return TimeSpan.Zero;
        }

        if (ContainsDate(startDate) && ContainsDate(responseDate))
        {
            return responseDate - startDate;
        }

        var adjustedStart = Later(workingHoursStartDate, startDate);
        var adjustedEnd = Earlier(workingHoursEndDate, responseDate);

        return adjustedEnd - adjustedStart;
    }

    // Returns the earlier of two dates.
    static DateTime Earlier(DateTime timeOnly, DateTime compare)
        => timeOnly < compare
            ? timeOnly
            : compare;

    // Returns the later of two dates.
    static DateTime Later(DateTime timeOnly, DateTime compare)
        => timeOnly > compare
            ? timeOnly
            : compare;

    record ResponseTimeDay(DateTime Start, DateTime End);

    record ResponseTimeRange(ResponseTimeDay Start, ResponseTimeDay? End = null, int DaysBetweenCount = 0);

    static ResponseTimeRange GetResponseTimeRange(DateTime start, DateTime end)
    {
        var startDayEnd = start.Date.AddDays(1).Date;

        if (end <= startDayEnd) // Start and end is the same day.
        {
            return new ResponseTimeRange(new ResponseTimeDay(start, end));
        }

        var startDay = new ResponseTimeDay(start, startDayEnd);
        var endDayStart = end.Date;
        var endDay = new ResponseTimeDay(endDayStart, end);
        var daysBetween = (int)(endDayStart - startDayEnd).TotalDays;
        return new ResponseTimeRange(startDay, endDay, daysBetween);
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NodaTime;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Used to help build up a query to records for the specified number of days prior to an end date considering
/// the time zone. This is similar to the <see cref="DateRangeSelector"/>, but selects date ranges that begin and end
/// at midnight of the specified timezone.
/// </summary>
/// <param name="Days">The number of days to query.</param>
/// <param name="EndDate">The end date for the date period in the specified <paramref name="Timezone"/>. The query returns records for the <paramref name="Days"/> number of days that occured on or before this date.</param>
/// <param name="Timezone">The time zone to use when selecting the range.</param>
public record DatePeriodSelector(int Days, LocalDate EndDate, DateTimeZone Timezone)
    : DateRangeSelector(Days, Timezone.AtStartOfDay(EndDate.PlusDays(1)).ToDateTimeUtc())
{
    /// <summary>
    /// Constructs a new <see cref="DateRangeSelector"/> that selects records for the specified number of days prior
    /// to the end date.
    /// </summary>
    /// <param name="days">The number of days to query.</param>
    /// <param name="clock">The clock used to retrieve the current UTC date as the end date.</param>
    /// <param name="timeZone">The time zone to use when selecting the range.</param>
    public DatePeriodSelector(int days, IClock clock, DateTimeZone timeZone)
        : this(days, clock.UtcNow.ToLocalDateTimeInTimeZone(timeZone).Date, timeZone)
    {
    }

    /// <summary>
    /// Groups the specified queryable by the specified date period.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>A queryable that groups entities by day.</returns>
    public IQueryable<IGrouping<DateTime, TEntity>> GroupByDay<TEntity>(IQueryable<TEntity> queryable)
        where TEntity : IEntity
    {
        return queryable.GroupBy(GroupByDayKeySelector<TEntity>());
    }

    /// <summary>
    /// Groups the specified queryable by the specified date period.
    /// </summary>
    /// <param name="enumerable">The queryable to apply the selector to.</param>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>A queryable that groups entities by day.</returns>
    public IOrderedEnumerable<IGrouping<LocalDate, TEntity>> GroupByLocalDate<TEntity>(IEnumerable<TEntity> enumerable)
        where TEntity : IEntity
    {
        return enumerable.GroupBy(e => GetLocalDateInTimeZone(e.Created))
            .EnsureGroups(EnumerateDays())
            .OrderBy(g => g.Key);
    }

    Expression<Func<TEntity, DateTime>> GroupByDayKeySelector<TEntity>() where TEntity : IEntity =>
        // Using TimeZoneInfo.ConvertTimeBySystemTimeZoneId is fine when it's part of an expression.
        // NpgSql will do the right thing. It's not fine outside an expression when running on Windows. :(
        e => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(e.Created, Timezone.Id).Date;

    // Convert a utc DateTime to a LocalDate in the specified time zone.
    LocalDate GetLocalDateInTimeZone(DateTime utcDate)
    {
        return utcDate.ToLocalDateTimeInTimeZone(Timezone).Date;
    }

    /// <summary>
    /// Returns a <see cref="DateOnly"/> for the number of days in this selector.
    /// </summary>
    /// <remarks>This is useful in cases where we need to render a graph for the past 7 days, but we don't
    /// have data for all seven days.</remarks>
    public IEnumerable<LocalDate> EnumerateDays()
    {
        return Enumerable.Range(1, Days).Select(i => EndDate.PlusDays(-1 * (Days - i)));
    }
}

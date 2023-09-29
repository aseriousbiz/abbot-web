using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Used to help build up a query that must select records within a given time period.
/// </summary>
/// <param name="StartDateTimeUtc">If specified, the query returns records that occurred on or after this date.</param>
/// <param name="EndDateTimeUtc">If specified, the query returns records that occured before this date.</param>
public record DateRangeSelector(DateTime StartDateTimeUtc, DateTime EndDateTimeUtc) : ISelector<MetricObservation>
{
    /// <summary>
    /// Return records for all time. In other words, a noop.
    /// </summary>
    public static DateRangeSelector AllTime => new(DateTime.MinValue, DateTime.MaxValue);

    protected DateRangeSelector(int daysAgo, DateTime endDateTimeUtc)
        : this(endDateTimeUtc.AddDays(-1 * daysAgo), endDateTimeUtc)
    {
    }

    /// <summary>
    /// Applies the date range to the given queryable, adding a where clause to include records within the date range.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <typeparam name="TEntity">The Entity type</typeparam>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> queryable) where TEntity : IEntity
    {
        if (StartDateTimeUtc == DateTime.MinValue && EndDateTimeUtc == DateTime.MaxValue)
        {
            return queryable;
        }
        return queryable.Where(e => e.Created >= StartDateTimeUtc && e.Created < EndDateTimeUtc);
    }

    /// <summary>
    /// Applies the date range to the given queryable, adding a where clause to include records within the date range.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable)
    {
        if (StartDateTimeUtc == DateTime.MinValue && EndDateTimeUtc == DateTime.MaxValue)
        {
            return queryable;
        }
        return queryable.Where(e => e.Timestamp >= StartDateTimeUtc && e.Timestamp < EndDateTimeUtc);

    }

    /// <summary>
    /// Adds a where clause to include records before the date range.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <typeparam name="TEntity">The Entity type</typeparam>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<TEntity> ApplyBefore<TEntity>(IQueryable<TEntity> queryable) where TEntity : IEntity
    {
        return queryable.Where(e => e.Created < StartDateTimeUtc);
    }
}

using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Extensions that make selectors a bit easier to chain selectors to <see cref="IQueryable{T}"/>.
/// </summary>
public static class SelectorExtensions
{
    /// <summary>
    /// Applies the given selector to the given queryable and returns the resulting queryable.
    /// </summary>
    /// <param name="queryable">The source queryable.</param>
    /// <param name="selector">The selector to apply.</param>
    /// <typeparam name="TEntity">The entity type of the queryable.</typeparam>
    /// <returns>A new queryable with the selector applied.</returns>
    public static IQueryable<TEntity> Apply<TEntity>(
        this IQueryable<TEntity> queryable,
        DateRangeSelector selector) where TEntity : IEntity
    {
        return selector.Apply(queryable);
    }

    /// <summary>
    /// Applies the given selector to the given queryable and returns the resulting queryable.
    /// </summary>
    /// <param name="queryable">The source queryable.</param>
    /// <param name="selector">The selector to apply.</param>
    /// <returns>A new queryable with the selector applied.</returns>
    public static IQueryable<T> Apply<T>(
        this IQueryable<T> queryable,
        ISelector<T> selector)
    {
        return selector.Apply(queryable);
    }

    /// <summary>
    /// Applies the given selector to the given queryable and returns the resulting queryable.
    /// </summary>
    /// <param name="queryable">The source queryable.</param>
    /// <param name="selector">The selector to apply.</param>
    /// <returns>A new queryable with the selector applied.</returns>
    public static IQueryable<TConversationEvent> Apply<TConversationEvent>(
        this IQueryable<TConversationEvent> queryable,
        RoomSelector selector) where TConversationEvent : ConversationEvent
    {
        return selector.Apply(queryable);
    }

    /// <summary>
    /// Applies the given selector to group the given queryable by day.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="datePeriodSelector"></param>
    /// <typeparam name="TSource"></typeparam>
    /// <returns></returns>
    public static IQueryable<IGrouping<DateTime, TSource>> GroupByDay<TSource>(
        this IQueryable<TSource> source,
        DatePeriodSelector datePeriodSelector) where TSource : IEntity
    {
        return datePeriodSelector.GroupByDay(source);
    }
}

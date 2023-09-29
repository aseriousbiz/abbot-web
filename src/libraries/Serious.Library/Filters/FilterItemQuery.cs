using System;
using System.Linq;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

/// <summary>
/// A filter item query is a query that can be applied to a filter.
/// </summary>
/// <typeparam name="T">The type this applies to.</typeparam>
public class FilterItemQuery<T> : IFilterItemQuery<T>
{
    readonly Func<IQueryable<T>, Filter, IQueryable<T>> _apply;

    /// <summary>
    /// Constructs a <see cref="FilterItemQuery{T}"/>.
    /// </summary>
    /// <param name="field">The field this applies to.</param>
    /// <param name="apply">The method that applies the filter to the query.</param>
    public FilterItemQuery(string field, Func<IQueryable<T>, Filter, IQueryable<T>> apply)
    {
        Field = field;
        _apply = apply;
    }

    /// <summary>
    /// The field this applies to.
    /// </summary>
    public string Field { get; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="query"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    public IQueryable<T> Apply(IQueryable<T> query, Filter filter)
    {
        return _apply(query, filter);
    }
}

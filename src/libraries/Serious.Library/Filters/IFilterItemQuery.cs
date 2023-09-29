using System.Linq;

namespace Serious.Filters;

/// <summary>
/// A queryable filter for a specific entity type.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IFilterItemQuery<T>
{
    /// <summary>
    /// The field this query filter applies to.
    /// </summary>
    string Field { get; }

    /// <summary>
    /// If <c>true</c>, then this filter can only be applied once. If two <see cref="Filter"/> instances in a query
    /// have this field, then the last one wins.
    /// </summary>
    bool Exclusive => false;

    /// <summary>
    /// Apply the filter to the query.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="filter">The filter to apply.</param>
    /// <returns>The resulting queryable.</returns>
    IQueryable<T> Apply(IQueryable<T> query, Filter filter);
}

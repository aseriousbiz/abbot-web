using System.Linq;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Used to help build up a query that filters queries when building up an EF Core query.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ISelector<T>
{
    /// <summary>
    /// Applies this selector to the given queryable.
    /// </summary>
    /// <param name="queryable">The queryable to filter.</param>
    IQueryable<T> Apply(IQueryable<T> queryable);
}


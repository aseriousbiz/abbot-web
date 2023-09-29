using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Serious.Filters;

/// <summary>
/// Applies all relevant filters to a query.
/// </summary>
public class QueryFilter<T>
{
    readonly IDictionary<string, IFilterItemQuery<T>> _queryFilters;

    public QueryFilter(IEnumerable<IFilterItemQuery<T>> queryFilters)
    {
        _queryFilters = queryFilters.ToDictionary(f => f.Field);

        ExclusiveFilterFields = _queryFilters
            .Values
            .Where(f => f.Exclusive)
            .Select(f => f.Field)
            .ToHashSet();
    }

    /// <summary>
    /// Applies all filters relevant to the passed in <see cref="FilterList" />.
    /// </summary>
    /// <param name="query">The original query.</param>
    /// <param name="filters">The filters to apply.</param>
    /// <param name="defaultField">The default field to use for filters that do not specify a field.</param>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>A new queryable with filtered results.</returns>
    public IQueryable<T> Apply(IQueryable<T> query, FilterList filters, string? defaultField = null)
    {
        if (!filters.Any())
        {
            return query;
        }
        var normalized = filters.NormalizeFilters(ExclusiveFilterFields);
        foreach (var filter in normalized)
        {
            var field = filter.Field ?? defaultField;

            if (field is not null && TryGetQueryFilter(field, out var queryFilter))
            {
                query = queryFilter.Apply(query, filter);
            }
        }

        return query;
    }

    HashSet<string> ExclusiveFilterFields { get; }

    bool TryGetQueryFilter([NotNullWhen(true)] string? field, [NotNullWhen(true)] out IFilterItemQuery<T>? queryFilter)
    {
        if (field is null)
        {
            queryFilter = null;
            return false;
        }

        if (!_queryFilters.TryGetValue(field, out var qf))
        {
            queryFilter = null;
            return false;
        }

        queryFilter = qf;
        return true;
    }
}

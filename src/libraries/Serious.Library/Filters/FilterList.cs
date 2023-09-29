using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#pragma warning disable CA1815

namespace Serious.Filters;

/// <summary>
/// A parsed search string.
/// </summary>
public struct FilterList : IReadOnlyList<Filter>, IParsable<FilterList>
{
    readonly bool _normalized;
    List<Filter>? _filters = new();

    public FilterList()
    {
    }

    public FilterList(IEnumerable<Filter> filters) : this(filters, false)
    {
    }

    FilterList(IEnumerable<Filter> filters, bool normalized)
    {
        _filters = filters.ToList();
        _normalized = normalized;
    }

    List<Filter> Filters
    {
        get {
            _filters ??= new List<Filter>();
            return _filters;
        }
    }

    public void Add(Filter filter) => Filters.Add(filter);

    public int Count => Filters.Count;

    public Filter this[int index] => Filters[index];

    public IEnumerator<Filter> GetEnumerator() => Filters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Join(" ", this);

    /// <summary>
    /// Returns a normalized version of the filter list.
    /// </summary>
    /// <remarks>
    /// Normalization means combining exclusive filters for the same field into a single filter (last one wins).
    /// </remarks>
    /// <param name="exclusiveFields">The set of fields that can only be applied once in a query.</param>
    public FilterList NormalizeFilters(IEnumerable<string> exclusiveFields)
    {
        if (_normalized)
        {
            return this;
        }

        var exclusiveFilters = exclusiveFields.ToHashSet();
        var seenFields = new HashSet<string>();
        var result = new List<Filter>();
        for (int i = Count - 1; i >= 0; i--)
        {
            var filter = this[i];
            if (filter.Field is null || !exclusiveFilters.Contains(filter.Field) || !seenFields.Contains(filter.Field))
            {
                result.Add(filter);
                if (filter.Field is not null && exclusiveFilters.Contains(filter.Field))
                {
                    seenFields.Add(filter.Field);
                }

            }
        }

        result.Reverse();
        return new FilterList(result, normalized: true);
    }

    /// <summary>
    /// Retrieves the value of the first filter with the specified field or <c>null</c> if no such filter exists.
    /// </summary>
    /// <param name="field">The field to retrieve.</param>
    public Filter? this[string field] => GetFilters(field).FirstOrDefault();

    /// <summary>
    /// Returns a new <see cref="FilterList" /> with all filters with the specified field removed and the new filter
    /// added.
    /// </summary>
    /// <param name="field">The field to search for.</param>
    /// <param name="newValue">The new value to apply. If <c>null</c>, then this filter is removed.</param>
    /// <returns>A new <see cref="FilterList"/>.</returns>
    public FilterList WithReplaced(string field, string? newValue)
    {
        var newFilter = Without(field);
        if (!string.IsNullOrWhiteSpace(newValue))
        {
            newFilter.Add(Filter.Create(field, newValue));
        }

        return newFilter;
    }

    /// <summary>
    /// Returns a new <see cref="FilterList" /> with all filters with the specified field removed.
    /// </summary>
    /// <param name="field">The field to search for.</param>
    /// <returns>A new <see cref="FilterList"/>.</returns>
    public FilterList Without(string field) => new(this.Except(GetFilters(field)));

    IEnumerable<Filter> GetFilters(string field)
    {
        var normalized = NormalizeField(field);
        return this.Where(f => f.Field == normalized);
    }

    static string NormalizeField(string field)
    {
        field = field.Trim().ToLowerInvariant();
        if (field is { Length: > 0 } && field[0] == '-')
        {
            field = field[1..];
        }

        return field;
    }

    public static FilterList Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var result)
        ? result
        : new FilterList();

    public static bool TryParse(string? s, IFormatProvider? provider, out FilterList result)
    {
        result = s is not null
            ? FilterParser.Parse(s)
            : new FilterList();

        return true;
    }

    /// <summary>
    /// Adds these filters to the list if there's no existing filter for the same field.
    /// </summary>
    /// <param name="filters"></param>
    /// <returns></returns>
    public FilterList WithDefaults(IEnumerable<Filter> filters)
    {
        var result = new FilterList(this);
        foreach (var filter in filters)
        {
            if (filter.Field is not null && result[filter.Field] is null)
            {
                result.Add(filter);
            }
        }

        return result;
    }
}

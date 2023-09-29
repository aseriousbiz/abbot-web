using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Extensions;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

/// <summary>
/// Class used to create filters for <see cref="MetricObservation"/>s.
/// </summary>
public static class MetricObservationFilters
{
    static IReadOnlyDictionary<string, Func<IQueryable<MetricObservation>, Filter, IQueryable<MetricObservation>>> Create() =>
        new Dictionary<string, Func<IQueryable<MetricObservation>, Filter, IQueryable<MetricObservation>>>
        {
            ["customer"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                c => c.Room!.Customer!.Name.ToLower() == filter.LowerCaseValue),

            ["segment"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                c => c.Room!.Customer!.TagAssignments.Any(t => t.Tag.Name.ToLower() == filter.LowerCaseValue)),
        };

    /// <summary>
    /// Creates and returns the set of <see cref="Conversation"/> filters.
    /// </summary>
    public static IEnumerable<IFilterItemQuery<MetricObservation>> CreateFilters() =>
        Create().Select(kvp => new FilterItemQuery<MetricObservation>(kvp.Key, kvp.Value));
}

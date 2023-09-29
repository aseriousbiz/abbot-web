using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Extensions;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

/// <summary>
/// Class used to create filters for customers.
/// </summary>
public static class CustomerFilters
{
    static IReadOnlyDictionary<string, Func<IQueryable<Customer>, Filter, IQueryable<Customer>>> Create() =>
        new Dictionary<string, Func<IQueryable<Customer>, Filter, IQueryable<Customer>>>
        {
            ["segment"] = (query, filter) =>
                filter.Value is "none"
                    ? query.WhereOrNot(filter.Include, c => !c.TagAssignments.Any()) // "none" basically means unassigned.
                    : query.WhereOrNot(
                        filter.Include,
                        c => c.TagAssignments.Any(a => a.Tag.Name.ToLower() == filter.LowerCaseValue)),

            ["room"] = (query, filter) =>
                filter.Value is "none"
                    ? query.WhereOrNot(filter.Include, c => !c.Rooms.Any()) // "none" basically means unassigned.
                    : query.WhereOrNot(
                        filter.Include,
                        c => c.Rooms.Any(a => a.PlatformRoomId.ToLower() == filter.LowerCaseValue)),
            ["search"] = (query, filter) =>
                filter.Value is { Length: > 0 }
                    ? query.WhereOrNot(filter.Include, c => c.Name.ToLower().Contains(filter.Value.ToLower()))
                    : query,
            ["activity"] = (query, filter) =>
                filter.Value switch {
                    "recent" when filter.Include => query.OrderByDescending(c => c.Rooms.Max(r => r.LastMessageActivityUtc)).Where(c => c.Rooms.Any(r => r.LastMessageActivityUtc != null)),
                    "recent" when !filter.Include => query.OrderBy(c => c.Rooms.Max(r => r.LastMessageActivityUtc)).Where(c => c.Rooms.Any(r => r.LastMessageActivityUtc != null)),
                    "none" => query.Where(c => c.Rooms.All(r => r.LastMessageActivityUtc == null)),
                    _ => query,
                }
        };

    /// <summary>
    /// Creates and returns the set of <see cref="Customer"/> filters.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<IFilterItemQuery<Customer>> CreateFilters() =>
        Create().Select(kvp => new FilterItemQuery<Customer>(kvp.Key, kvp.Value));
}

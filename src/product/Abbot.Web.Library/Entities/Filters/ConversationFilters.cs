using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Extensions;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

/// <summary>
/// Class used to create filters for <see cref="Conversation"/>s.
/// </summary>
public static class ConversationFilters
{
    static IReadOnlyDictionary<string, Func<IQueryable<Conversation>, Filter, IQueryable<Conversation>>> Create() =>
        new Dictionary<string, Func<IQueryable<Conversation>, Filter, IQueryable<Conversation>>>
        {
            ["customer"] = (query, filter) => query.WhereOrNot(
                    filter.Include,
                    c => c.Room.Customer!.Name.ToLower() == filter.LowerCaseValue),

            ["segment"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                c => c.Room.Customer!.TagAssignments.Any(t => t.Tag.Name.ToLower() == filter.LowerCaseValue)),
        };

    /// <summary>
    /// Creates and returns the set of <see cref="Conversation"/> filters.
    /// </summary>
    public static IEnumerable<IFilterItemQuery<Conversation>> CreateFilters() =>
        Create().Select(kvp => new FilterItemQuery<Conversation>(kvp.Key, kvp.Value));
}

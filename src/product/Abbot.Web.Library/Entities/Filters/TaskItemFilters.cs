using System.Collections.Generic;
using System.Linq;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

public static class TaskItemFilters
{
    static IReadOnlyDictionary<string, Func<IQueryable<TaskItem>, Filter, IQueryable<TaskItem>>> Create() =>
        new Dictionary<string, Func<IQueryable<TaskItem>, Filter, IQueryable<TaskItem>>>
        {
            ["is"] = (query, filter) => {
                var statusFilter = Enum.TryParse<TaskItemStatus>(filter.Value, out var status)
                    ? status
                    : TaskItemStatus.None;

                return filter.Include
                    ? query.Where(t => t.Properties.Status == statusFilter
                                       || (statusFilter == TaskItemStatus.None
                                           && t.Properties.Status != TaskItemStatus.Closed))
                    : query.Where(t => t.Properties.Status != statusFilter);
            },

            ["assignee"] = (query, filter) => (filter.LowerCaseValue, filter.Include) switch {
                ("none", _) => query.Where(t => t.Assignee == null),
                (_, true) => query.Where(t => t.Assignee!.User.PlatformUserId.ToLower() == filter.LowerCaseValue),
                (_, false) => query.Where(t => t.Assignee!.User.PlatformUserId.ToLower() != filter.LowerCaseValue),
            },

            ["customer"] = (query, filter) => filter.Include
                ? query.Where(t => t.Customer!.Name.ToLower() == filter.LowerCaseValue)
                : query.Where(t => t.Customer!.Name.ToLower() != filter.LowerCaseValue),

            ["segment"] = (query, filter) => filter.Include
                ? query.Where(t => t.Customer!.TagAssignments.Any(a => a.Tag.Name == filter.LowerCaseValue))
                : query.Where(t => t.Customer!.TagAssignments.All(a => a.Tag.Name != filter.LowerCaseValue))

        };

    /// <summary>
    /// Creates and returns the set of room filters.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<IFilterItemQuery<TaskItem>> CreateFilters() =>
        Create().Select(kvp => new FilterItemQuery<TaskItem>(kvp.Key, kvp.Value));
}

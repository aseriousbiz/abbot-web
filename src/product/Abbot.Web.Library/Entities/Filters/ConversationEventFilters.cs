using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Extensions;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

/// <summary>
/// Class used to create filters for <see cref="Conversation"/>s.
/// </summary>
public static class ConversationEventFilters
{
    static IReadOnlyDictionary<string, Func<IQueryable<TConversationEvent>, Filter, IQueryable<TConversationEvent>>> Create<TConversationEvent>()
        where TConversationEvent : ConversationEvent =>
        new Dictionary<string, Func<IQueryable<TConversationEvent>, Filter, IQueryable<TConversationEvent>>>
        {
            ["customer"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                e => e.Conversation.Room.Customer!.Name.ToLower() == filter.LowerCaseValue),

            ["segment"] = (query, filter) => query.WhereOrNot(
                filter.Include,
                e => e.Conversation.Room.Customer!.TagAssignments.Any(t => t.Tag.Name.ToLower() == filter.LowerCaseValue)),
        };

    /// <summary>
    /// Creates and returns the set of <see cref="Conversation"/> filters.
    /// </summary>
    public static IEnumerable<IFilterItemQuery<TConversationEvent>> CreateFilters<TConversationEvent>()
        where TConversationEvent : ConversationEvent =>
        Create<TConversationEvent>().Select(kvp => new FilterItemQuery<TConversationEvent>(kvp.Key, kvp.Value));
}

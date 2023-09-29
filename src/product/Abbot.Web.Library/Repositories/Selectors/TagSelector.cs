using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Used to help build up a query that filters conversations by a tag.
/// </summary>
public record TagSelector(string Tag) : ISelector<Conversation>, ISelector<MetricObservation>, ISelector<StateChangedEvent>, ISelector<MessagePostedEvent>
{
    public const string AllTagSelectorToken = "!all"; // ! is not valid in tag names, so we can use this as our "all" indicator.

    /// <summary>
    /// Creates a tag selector from the specified tag.
    /// </summary>
    /// <param name="tag">The tag to filter on. Use <c>null</c> to filter on all tags.</param>
    public static TagSelector Create(string? tag) => tag is null or AllTagSelectorToken or ""
        ? All
        : new(tag);

    /// <summary>
    /// Return records for all time. In other words, a noop.
    /// </summary>
    public static TagSelector All => new(AllTagSelectorToken);

    /// <summary>
    /// Applies the tag filter to the given queryable, adding a where clause to include conversations with the
    /// specified tag.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<Conversation> Apply(IQueryable<Conversation> queryable)
        => Tag is AllTagSelectorToken ? queryable : queryable.Where(c => c.Tags.Any(t => t.Tag.Name == Tag));

    /// <summary>
    /// Applies the tag filter to the given queryable, adding a where clause to include conversations with the
    /// specified tag.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable)
        => Tag is AllTagSelectorToken ? queryable : queryable.Where(c => c.Conversation!.Tags.Any(t => t.Tag.Name == Tag));

    /// <summary>
    /// Applies the tag filter to the given queryable, adding a where clause to include conversations with the
    /// specified tag.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<StateChangedEvent> Apply(IQueryable<StateChangedEvent> queryable)
        => Apply<StateChangedEvent>(queryable);

    /// <summary>
    /// Applies the tag filter to the given queryable, adding a where clause to include conversations with the
    /// specified tag.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public IQueryable<MessagePostedEvent> Apply(IQueryable<MessagePostedEvent> queryable)
        => Apply<MessagePostedEvent>(queryable);

    /// <summary>
    /// Applies the tag filter to the given queryable, adding a where clause to include conversations with the
    /// specified tag.
    /// </summary>
    /// <param name="queryable">The queryable to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> queryable)
        where TConversationEvent : ConversationEvent
        => Tag is AllTagSelectorToken ? queryable : queryable.Where(c => c.Conversation.Tags.Any(t => t.Tag.Name == Tag));

}

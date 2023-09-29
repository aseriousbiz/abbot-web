using System.Collections.Generic;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a set of statistics about a collection of conversations.
/// </summary>
/// <param name="CountByState">The count of conversations grouped by the current state of the conversation.</param>
/// <param name="TotalCount">The total number of conversations.</param>
public record ConversationStats(IReadOnlyDictionary<ConversationState, int> CountByState, int TotalCount);

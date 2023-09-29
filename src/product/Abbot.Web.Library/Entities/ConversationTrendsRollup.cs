namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a summary of the relevant Conversation metrics for a specific time period, given a specific user/room/etc.
/// </summary>
/// <param name="Start">
/// The start of the rollup period (in UTC).
/// The rollup includes all values including and after this time.
/// </param>
/// <param name="End">
/// The end of the rollup period (in UTC).
/// The rollup includes all values up to BUT EXCLUDING this time.
/// </param>
/// <param name="AverageTimeToFirstResponse">
/// The average time to first response over the roll-up period.
/// If <c>null</c>, then no conversations moved from New to Waiting in the roll-up period.
/// </param>
/// <param name="AverageTimeToResponse">
/// The average time to response over the roll-up period.
/// If <c>null</c>, then no conversations moved from New or NeedsAttention to Waiting in the roll-up period.
/// </param>
/// <param name="AverageTimeToFirstResponseDuringCoverage">
/// The average time to first response over the roll-up period only counting time elapsed during
/// covered hours.
/// If <c>null</c>, then no conversations moved from New to Waiting in the roll-up period.
/// </param>
/// <param name="AverageTimeToResponseDuringCoverage">
/// The average time to response over the roll-up period only counting time elapsed during
/// covered hours.
/// If <c>null</c>, then no conversations moved from New or NeedsAttention to Waiting in the roll-up period.
/// </param>
/// <param name="AverageTimeToClose">
/// The average time to close over the roll-up period.
/// If <c>null</c>, then no conversations were closed in the roll-up period.
/// </param>
/// <param name="PercentWithinTarget">
/// The percent of conversations that were within the target time to response.
/// </param>
/// <param name="NewConversations">
/// The total number of new conversations in the roll-up period.
/// If <c>null</c>, then no conversations were created in the roll-up period.
/// </param>
public record ConversationTrendsRollup(
    DateTime Start,
    DateTime End,
    TimeSpan? AverageTimeToFirstResponse,
    TimeSpan? AverageTimeToResponse,
    TimeSpan? AverageTimeToFirstResponseDuringCoverage,
    TimeSpan? AverageTimeToResponseDuringCoverage,
    TimeSpan? AverageTimeToClose,
    int? PercentWithinTarget,
    int? NewConversations);

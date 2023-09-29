using System.Collections.Generic;
using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Some usage stats for the customer.
/// </summary>
/// <param name="TopTags">The top tags used by the customer.</param>
/// <param name="TrendSummary">Summary information about trends.</param>
/// <param name="InsightsSummary">Summary information about conversations.</param>
/// <param name="StartDate">Start date.</param>
/// <param name="EndDate">End date.</param>
public record CustomerUsageStats(
    IReadOnlyList<TagFrequencyInfo> TopTags,
    TrendsSummary TrendSummary,
    InsightsSummaryInfo InsightsSummary,
    LocalDate StartDate,
    LocalDate EndDate);

/// <summary>
/// Represents statistics for the insights page.
/// </summary>
/// <param name="WentOverdueCount">The count of conversations went overdue in the relevant period.</param>
/// <param name="OpenedCount">The count of conversations created by foreign members in the relevant period.</param>
/// <param name="NeededAttentionCount">The count of distinct conversations were in need of attention at any time during the period.</param>
/// <param name="RespondedCount">The number of conversations in which a first-responder responded to the message during the relevant period.</param>
/// <param name="OpenedConversationsRoomCount">Number of rooms in which conversations were opened during the relevant period.</param>
public record InsightsStats(
    int WentOverdueCount,
    int OpenedCount,
    int NeededAttentionCount,
    int RespondedCount,
    int OpenedConversationsRoomCount);

/// <summary>
/// Response time summaries.
/// </summary>
/// <param name="AverageTimeToFirstResponse"></param>
/// <param name="AverageTimeToResponse"></param>
/// <param name="AverageTimeToFirstResponseDuringCoverage"></param>
/// <param name="AverageTimeToResponseDuringCoverage"></param>
/// <param name="AverageTimeToClose"></param>
/// <param name="NewConversations"></param>
public record TrendsSummary(
    double? AverageTimeToFirstResponse,
    double? AverageTimeToResponse,
    double? AverageTimeToFirstResponseDuringCoverage,
    double? AverageTimeToResponseDuringCoverage,
    double? AverageTimeToClose,
    int? NewConversations);

/// <summary>
/// Contains summary counts for the Insights page.
/// </summary>
/// <param name="OverdueCount">The number of distinct conversations that were in an overdue state during the period.</param>
/// <param name="NeedsAttention">The number of distinct conversations that needed a response from a first-responder during the period.</param>
/// <param name="RespondedCount">The number of distinct conversations that received a response from a first-responder during the period.</param>
/// <param name="OpenConversations">The number of distinct conversations that were in an open state during the period.</param>
/// <param name="TicketsCreated">The number of tickets opened from these conversations.</param>
/// <param name="OpenedConversationsRoomCount">Number of rooms in which conversations were opened during the relevant period.</param>
public record
    InsightsSummaryInfo(
    int OverdueCount,
    int NeedsAttention,
    int RespondedCount,
    int OpenConversations,
    int TicketsCreated,
    int OpenedConversationsRoomCount);

/// <summary>
/// Information about the number of times a tag has been used.
/// </summary>
/// <param name="Tag">The tag name.</param>
/// <param name="Count">The number of times it was used.</param>
public record TagFrequencyInfo(string Tag, int Count);

using System.Collections.Generic;
using NodaTime;

namespace Serious.Abbot.Models.Api;

/// <summary>
/// The response model for returning data for the Insights Conversation Volume bar graph.
/// </summary>
/// <param name="TimeZone">The time zone used for the data.</param>
/// <param name="Data">The set of data to render.</param>
public record ConversationVolumeResponseModel(
    string TimeZone,
    IReadOnlyList<ConversationVolumePeriod> Data);


/// <summary>
/// Represents the stats for a given period (day, week, etc.) for the conversations volume
/// bar graph.
/// </summary>
/// <param name="Date">The date for this metric.</param>
/// <param name="Open">The number of open conversations that were open at any time during the period.</param>
/// <param name="New">The number of new conversations during this period.</param>
/// <param name="Overdue">Number of conversations that were overdue any time during that period OR the number of conversations that became overdue during that period.</param>
public record ConversationVolumePeriod(
    LocalDate Date,
    int? Overdue,
    int? New,
    int? Open);

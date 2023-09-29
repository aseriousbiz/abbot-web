using System.Collections.Generic;

namespace Serious.Abbot.Models.Api;

/// <summary>
/// Information about the trends for a customer.
/// </summary>
/// <param name="TimeZone">The timezone.</param>
/// <param name="Summary">A summary.</param>
/// <param name="Data">The trend data.</param>
public record TrendsResponseModel(
    string TimeZone,
    TrendsSummary Summary,
    IReadOnlyList<TrendsDay> Data);

/// <summary>
/// A single day
/// </summary>
/// <param name="Date"></param>
/// <param name="AverageTimeToFirstResponse"></param>
/// <param name="AverageTimeToResponse"></param>
/// <param name="AverageTimeToFirstResponseDuringCoverage"></param>
/// <param name="AverageTimeToResponseDuringCoverage"></param>
/// <param name="AverageTimeToClose"></param>
/// <param name="PercentWithinTarget"></param>
/// <param name="NewConversations"></param>
public record TrendsDay(
    DateTime Date,
    double? AverageTimeToFirstResponse,
    double? AverageTimeToResponse,
    double? AverageTimeToFirstResponseDuringCoverage,
    double? AverageTimeToResponseDuringCoverage,
    double? AverageTimeToClose,
    int? PercentWithinTarget,
    int? NewConversations);

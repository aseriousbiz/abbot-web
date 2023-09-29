using System;
using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a daily roll up of metrics.
/// </summary>
public class DailyMetricsRollup
{
    /// <summary>
    /// The date of the roll-up.
    /// </summary>
    [Key]
    public DateTime Date { get; set; }

    /// <summary>
    /// The number of distinct active users on this day.
    /// </summary>
    public int ActiveUserCount { get; set; }

    /// <summary>
    /// The number of human interactions (non-scheduled) on this day.
    /// </summary>
    public int InteractionCount { get; set; }

    /// <summary>
    /// The number of total skills created on this day.
    /// </summary>
    public int SkillCreatedCount { get; set; }

    /// <summary>
    /// The number of total organizations created on this day.
    /// </summary>
    public int OrganizationCreatedCount { get; set; }

    /// <summary>
    /// The number of total users created on this day.
    /// </summary>
    public int UserCreatedCount { get; set; }

    /// <summary>
    /// The MRR on this particular day.
    /// </summary>
    public decimal MonthlyRecurringRevenue { get; set; }
}

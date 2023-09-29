using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event when a skill is triggered by a scheduled event.
/// </summary>
public class ScheduledTriggerRunEvent : TriggerRunEvent
{
    /// <summary>
    /// The schedule as a cron string.
    /// </summary>
    [Column(nameof(CronSchedule))]
    public string CronSchedule { get; set; } = string.Empty;

    /// <summary>
    /// The TimeZoneId for the schedule, if needed.
    /// </summary>
    [Column(nameof(TimeZoneId))]
    public string? TimeZoneId { get; set; }
}

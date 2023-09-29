using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public class ScheduledTriggerChangeEvent : TriggerChangeEvent
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

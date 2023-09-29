using System;
using System.Diagnostics;

namespace Serious.Abbot.Entities;

[DebuggerDisplay("{Date} {TeamId} {EventType} {SuccessCount} {ErrorCount}")]
public class SlackEventsRollup
{
    /// <summary>
    /// The date of the roll-up.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// The Event type.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// The Slack Team Id.
    /// </summary>
    public string TeamId { get; set; } = null!;

    /// <summary>
    /// The number of successful events for the given date, event type, and team.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// The number of error events for the given date, event type, and team.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// The number of incomplete Slack events. These are events we didn't complete processing but also didn't
    /// error out. Could be a Hangfire issue.
    /// </summary>
    public int IncompleteCount { get; set; }
}

using System;
using Serious.Cryptography;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an incoming message from Slack.
/// </summary>
public class SlackEvent : EntityBase<SlackEvent>
{
    /// <summary>
    /// The slack event Id. We use this to ensure events are idempotent.
    /// </summary>
    public string EventId { get; set; } = null!;

    /// <summary>
    /// The Slack event type. This is not the type of the outer event envelope (which is always "event_callback"),
    /// but the type of the inner Event payload.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// The Slack App Id the event was sent to.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// The Team Id the incoming event was sent to.
    /// </summary>
    public string TeamId { get; set; } = null!;

    /// <summary>
    /// The Id of job that is handling this event. For us, this is the Hangfire Job Id.
    /// </summary>
    public string? JobId { get; set; }

    /// <summary>
    /// The UTC time at which the job last started processing.
    /// </summary>
    public DateTime? StartedProcessing { get; set; }

    /// <summary>
    /// The content of the event. In our case, this is a serialized Activity.
    /// </summary>
    public SecretString Content { get; set; } =
        null!; // No need to schedule key migration for this property because it's intended to be short lived.

    /// <summary>
    /// When processing the event was completed.
    /// </summary>
    public DateTime? Completed { get; set; }

    /// <summary>
    /// If an error occurred processing this, what was the error?
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The number of attempts we've made to processes this event.
    /// </summary>
    public int ProcessingAttempts { get; set; }
}

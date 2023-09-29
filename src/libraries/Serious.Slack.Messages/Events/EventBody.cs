using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;

namespace Serious.Slack.Events;

/// <summary>
/// Represents a Slack Event object https://api.slack.com/events-api#receiving_events. This is usually wrapped in
/// an <see cref="IEventEnvelope{T}"/> instance.
/// </summary>
public abstract record EventBody : Element
{
    /// <summary>
    /// Constructs an <see cref="EventBody"/>.
    /// </summary>
    /// <param name="type">The event <c>type</c>.</param>
    protected EventBody(string type) : base(type)
    {
    }

    /// <summary>
    /// The timestamp of the event. The combination of <c>event_ts</c>, <c>team_id</c>, <c>user_id</c>,
    /// or <c>channel_id</c> is intended to be unique. This field is included with every inner event type.
    /// </summary>
    /// <remarks>
    /// Ex. 1469470591.759709
    /// </remarks>
    [JsonProperty("event_ts")]
    [JsonPropertyName("event_ts")]
    public string EventTimestamp { get; init; } = null!;

    /// <summary>
    /// The user ID belonging to the user that incited this action. Not included in all events as not all events are
    /// controlled by users. See the top-level callback object's authed_users if you need to calculate event
    /// visibility by user.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>
    /// The timestamp of what the event describes, which may occur slightly prior to the event being dispatched as
    /// described by <c>event_ts</c>. The combination of <c>ts</c>, <c>team_id</c>, <c>user_id</c>,
    /// or <c>channel_id</c> is intended to be unique.
    /// </summary>
    /// <remarks>
    /// Ex. 1469470591.759709
    /// </remarks>
    [JsonProperty("ts")]
    [JsonPropertyName("ts")]
    public string? Timestamp { get; init; }

    /// <summary>
    /// The timestamp of the related thread, if any.
    /// </summary>
    [JsonProperty("thread_ts")]
    [JsonPropertyName("thread_ts")]
    public string? ThreadTs { get; init; }

    /// <summary>
    /// Not sure what this is.
    /// </summary>
    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; init; }
}

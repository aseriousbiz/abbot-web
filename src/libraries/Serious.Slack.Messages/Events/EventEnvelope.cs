using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Represents an incoming from the <see href="https://api.slack.com/apis/connections/events-api">Event API</see>.
/// Every event is wrapped in an <see href="https://api.slack.com/types/event">event wrapper</see>.
/// </summary>
[Element("event_callback")]
public record EventEnvelope<TEvent>() : Element("event_callback"), IEventEnvelope<TEvent> where TEvent : EventBody
{
    /// <summary>
    /// The event.
    /// </summary>
    [JsonProperty("event")]
    [JsonPropertyName("event")]
    public TEvent Event { get; init; } = null!;

    /// <summary>
    /// The shared-private callback token that authenticates this callback to the application as having come from Slack.
    /// Match this against what you were given when the subscription was created. If it does not match, do not process
    /// the event and discard it.
    /// </summary>
    [JsonProperty("token")]
    [JsonPropertyName("token")]
    public string Token { get; init; } = null!;

    /// <summary>
    /// The unique identifier for the workspace/team where this event occurred.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string? TeamId { get; init; }

    /// <summary>
    /// The unique identifier for the enterprise where this event occurred, if it occured within an Enterprise Slack.
    /// </summary>
    [JsonProperty("enterprise_id")]
    [JsonPropertyName("enterprise_id")]
    public string? EnterpriseId { get; init; }

    /// <summary>
    /// The unique identifier for the application this event is intended for. Your application's ID can be found in
    /// the URL of the your application console. If your Request URL manages multiple applications, use this field
    /// along with the token field to validate and route incoming requests.
    /// </summary>
    [JsonProperty("api_app_id")]
    [JsonPropertyName("api_app_id")]
    public string ApiAppId { get; init; } = null!;

    /// <summary>
    /// The authorizations associated with this message. This should include Abbot's real user id.
    /// </summary>
    [JsonProperty("authorizations")]
    [JsonPropertyName("authorizations")]
    public IReadOnlyList<Authorization>? Authorizations { get; init; } = Array.Empty<Authorization>();

    /// <summary>
    /// Whether this message is in an externally shared channel or not.
    /// </summary>
    [JsonProperty("is_ext_shared_channel")]
    [JsonPropertyName("is_ext_shared_channel")]
    public bool IsExternallySharedChannel { get; set; }

    /// <summary>
    /// An identifier for this specific event. This field can be used with the
    /// <see href="https://api.slack.com/methods/apps.event.authorizations.list">apps.event.authorizations.list</see>
    /// method to obtain a full list of installations of your app that this event is visible to.
    /// </summary>
    [JsonProperty("event_context")]
    [JsonPropertyName("event_context")]
    public string? EventContext { get; init; }

    /// <summary>
    /// A unique identifier for this specific event, globally unique across all workspaces. Ex. Ev02SWS99K7W
    /// </summary>
    [JsonProperty("event_id")]
    [JsonPropertyName("event_id")]
    public string EventId { get; init; } = null!;

    /// <summary>
    /// The epoch timestamp in seconds indicating when this event was dispatched.
    /// </summary>
    [JsonProperty("event_time")]
    [JsonPropertyName("event_time")]
    public long EventTime { get; init; }
}

using System.Collections.Generic;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Wraps an incoming event from the <see href="https://api.slack.com/apis/connections/events-api">Event API</see> with
/// metadata about the event as described in the <see href="https://api.slack.com/types/event">event wrapper docs</see>.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(ElementConverter))]
public interface IEventEnvelope<out TEvent> : IEventEnvelope where TEvent : EventBody
{
    /// <summary>
    /// The actual event.
    /// </summary>
    TEvent Event { get; }
}

/// <summary>
/// Wraps an incoming event from the <see href="https://api.slack.com/apis/connections/events-api">Event API</see> with
/// metadata about the event as described in the <see href="https://api.slack.com/types/event">event wrapper docs</see>.
/// </summary>
/// <remarks>
/// This does not include the event payload. Cast to the specific <see cref="IEventEnvelope{TEvent}"/> type.
/// </remarks>
[Newtonsoft.Json.JsonConverter(typeof(ElementConverter))]
public interface IEventEnvelope : IElement
{
    /// <summary>
    /// The authorizations associated with this message. This should include Abbot's real user id.
    /// </summary>
    IReadOnlyList<Authorization>? Authorizations { get; }

    /// <summary>
    /// The shared-private callback token that authenticates this callback to the application as having come from Slack.
    /// Match this against what you were given when the subscription was created. If it does not match, do not process
    /// the event and discard it.
    /// </summary>
    string Token { get; }

    /// <summary>
    /// The unique identifier for the enterprise where this event occurred, if it occured within an Enterprise Slack.
    /// </summary>
    string? EnterpriseId { get; }

    /// <summary>
    /// Whether this message is in an externally shared channel or not.
    /// </summary>
    bool IsExternallySharedChannel { get; }

    /// <summary>
    /// An identifier for this specific event. This field can be used with the
    /// <see href="https://api.slack.com/methods/apps.event.authorizations.list">apps.event.authorizations.list</see>
    /// method to obtain a full list of installations of your app that this event is visible to.
    /// </summary>
    string? EventContext { get; }

    /// <summary>
    /// A unique identifier for this specific event, globally unique across all workspaces. Ex. Ev02SWS99K7W
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// The epoch timestamp in seconds indicating when this event was dispatched.
    /// </summary>
    long EventTime { get; init; }
}

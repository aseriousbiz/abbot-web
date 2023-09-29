using Serious.Abbot.Entities;
using Serious.Slack.Events;

namespace Serious.Abbot.Eventing.Messages;

public record SlackEventReceived
{
    /// <summary>
    /// The Slack event itself.
    /// </summary>
    public required IEventEnvelope<EventBody> Envelope { get; init; }

    /// <summary>
    /// The event type. This is the same as <see cref="IEventEnvelope{EventBody}.Event.Type"/>, but as a first-class property to allow for simpler routing later.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// The integration ID of the custom Slack Bot integration this message was received from.
    /// </summary>
    public required Id<Integration>? IntegrationId { get; init; }

    /// <summary>
    /// The Slack Team ID of the event. Useful for routing and session management in the future.
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// The retry number for this event.
    /// 0 means this is the first time we've seen this event.
    /// </summary>
    public required int RetryNumber { get; init; }
}

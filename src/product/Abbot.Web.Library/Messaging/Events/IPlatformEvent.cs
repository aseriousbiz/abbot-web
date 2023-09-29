using System;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Events;

/// <summary>
/// Represents a normalized event that occurred on a chat platform with a strongly typed payload.
/// </summary>
public interface IPlatformEvent : IFeatureActor
{
    /// <summary>
    /// The event payload.
    /// </summary>
    /// <remarks>
    /// This is an object because it sometimes is a payload from the underlying platform (as opposed to one of our
    /// "translated" types in Serious.Abbot.Events). One example is when a user opens the App Home in Slack. That
    /// event is unique to Slack so we want the original payload since we're not going to translate it.
    /// </remarks>
    object? Payload { get; }

    /// <summary>
    /// Used to respond to chat.
    /// </summary>
    IResponder Responder { get; }

    /// <summary>
    /// The date and time when this message was received. Not to be confused with the Slack timestamp which can be used to
    /// identify a Slack message.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The Bot User.
    /// </summary>
    BotChannelUser Bot { get; }

    /// <summary>
    /// The <see cref="Member"/> that initiated the event or message.
    /// </summary>
    Member From { get; }

    /// <summary>
    /// The organization that is the source of this event.
    /// </summary>
    Organization Organization { get; }

    /// <summary>
    /// A short-lived ID that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see> in Slack.
    /// </summary>
    string? TriggerId { get; }

    /// <summary>
    /// The room where this event originated, if applicable.
    /// </summary>
    Room? Room { get; }
}

/// <summary>
/// Represents a strongly typed normalized event that occurred on a chat platform with a strongly typed payload.
/// </summary>
/// <typeparam name="TPayload">The payload type.</typeparam>
public interface IPlatformEvent<out TPayload> : IPlatformEvent
{
    /// <summary>
    /// The event payload.
    /// </summary>
    new TPayload Payload { get; }
}

public interface IRoomPayload
{
    /// <summary>
    /// Gets the platform-specific ID of the room.
    /// </summary>
    string PlatformRoomId { get; }
}

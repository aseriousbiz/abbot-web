using System;
using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Information about a chat room (Slack only at this time).
/// </summary>
public interface IRoomInfo : IRoom
{
    /// <summary>
    /// The topic of the chat room.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// The purpose of the chat room.
    /// </summary>
    string Purpose { get; }
}

/// <summary>
/// Detailed information about a chat room such as the first responders, the escalation responders,
/// response times settings, etc.
/// </summary>
public interface IRoomDetails : IRoom
{
    /// <summary>
    /// Whether conversation tracking is enabled in this room.
    /// </summary>
    bool ConversationTrackingEnabled { get; }

    /// <summary>
    /// Whether or not Abbot is a member of the room. It's null if it is not yet known.
    /// </summary>
    bool? BotIsMember { get; }

    /// <summary>
    /// The conversation tracking response settings for the room. If any of the properties are empty, check the
    /// <see cref="DefaultResponseSettings"/> for the corresponding organizational default value.
    /// </summary>
    IResponseSettings ResponseSettings { get; }

    /// <summary>
    /// The default response settings for the organization.
    /// </summary>
    IResponseSettings DefaultResponseSettings { get; }

    /// <summary>
    /// Custom metadata associated with the room.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>
/// Settings related to conversation tracking response times and responders.
/// </summary>
public interface IResponseSettings
{
    /// <summary>
    /// The response time thresholds.
    /// </summary>
    Threshold<TimeSpan> ResponseTime { get; }

    /// <summary>
    /// The set of first responders.
    /// </summary>
    IReadOnlyList<IChatUser> FirstResponders { get; }

    /// <summary>
    /// The set of escalation responders.
    /// </summary>
    IReadOnlyList<IChatUser> EscalationResponders { get; }
}



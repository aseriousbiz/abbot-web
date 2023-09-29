using System;
using System.Collections.Generic;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a message in the chat platform.
/// </summary>
public interface IMessage
{
    /// <summary>
    /// The text of the message.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// The platform-specific Id of the message. For Slack, this is the timestamp.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The URL to the message.
    /// </summary>
    Uri Url { get; }

    /// <summary>
    /// The thread id of the message, if it's in a thread.
    /// </summary>
    string? ThreadId { get; }

    /// <summary>
    /// The author that sent the message.
    /// </summary>
    IChatUser Author { get; }
}

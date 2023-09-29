using System;
using System.Collections.Generic;
using Serious.Abbot.Models;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Messages;

public record SourceMessageInfo
{
    /// <summary>
    /// The text of the message.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The timestamp of the message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The thread id of the message, if it's in a thread.
    /// </summary>
    public required string? ThreadId { get; init; }

    /// <summary>
    /// The URL to the message.
    /// </summary>
    public required Uri MessageUrl { get; init; }

    /// <summary>
    /// The author that sent the message.
    /// </summary>
    public required PlatformUser Author { get; init; }
};

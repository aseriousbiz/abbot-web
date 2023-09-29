using Serious.Slack.Abstractions;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Represents the Slack specific information in the ChannelData of an incoming message or event.
/// </summary>
public class SlackChannelData
{
    /// <summary>
    /// The payload from an interactive element.
    /// </summary>
    public Payload? Payload { get; init; }

    /// <summary>
    /// The message information of the channel data. At this point we don't know what the type should
    /// be until we examine it.
    /// </summary>
    public IElement? SlackMessage { get; init; }

    /// <summary>
    /// The Slack Bot Token
    /// </summary>
    public string ApiToken { get; init; } = null!;
}

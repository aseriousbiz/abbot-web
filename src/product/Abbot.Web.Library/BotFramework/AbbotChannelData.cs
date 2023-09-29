using Microsoft.Bot.Schema;

namespace Serious.Abbot.BotFramework;

/// <summary>
/// Abbot-specific metadata for outgoing messages. This is generally smuggled via <see cref="IActivity.ChannelData"/>.
/// </summary>
public class AbbotChannelData
{
    /// <summary>
    /// Gets or sets the conversation to send the activity on, overriding <see cref="IActivity.Conversation"/>.
    /// </summary>
    public IMessageTarget? OverriddenMessageTarget { get; set; }

    /// <summary>
    /// Gets or sets the channel data that was active before the <see cref="AbbotChannelData"/> was attached.
    /// </summary>
    public object? InnerChannelData { get; set; }

    public AbbotChannelData(object? innerChannelData)
    {
        InnerChannelData = innerChannelData;
    }
}

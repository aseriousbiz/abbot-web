using Microsoft.Bot.Schema;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Interface that wraps a <see cref="ChannelAccount" /> and represents a user on the chat platform, but
/// transforms the channel account data to Abbot's needs.
/// </summary>
public interface IChannelUser
{
    /// <summary>
    /// The ID of the user on their chat platform.
    /// </summary>
    string Id { get; }
}

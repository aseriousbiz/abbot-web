using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Wraps a <see cref="ChannelAccount" /> and represents a user on the chat platform.
/// It transforms the data in a <see cref="ChannelAccount" /> to Abbot's needs.
/// </summary>
/// <remarks>
/// <para>
/// When creating an <see cref="MessageContext" />, we map a <see cref="ChannelUser" /> to the
/// corresponding <see cref="Member"/>.
/// </para>
/// <para>
/// This should not be confused with a <see cref="PlatformUser"/>. A <see cref="PlatformUser"/>
/// represents the serialization format for sending information about a <see cref="Member"/>
/// to the skill runners.
/// </para>
/// </remarks>
public abstract class ChannelUser : IChannelUser
{
    /// <summary>
    /// Constructs a <see cref="ChannelUser" /> with the specified platform user id.
    /// </summary>
    /// <param name="platformId">The platform-specific organization ID</param>
    /// <param name="platformUserId">The platform user id.</param>
    protected ChannelUser(string? platformId, string platformUserId)
    {
        Id = platformUserId;
        PlatformId = platformId;
    }

    /// <summary>
    /// The ID of the user on their chat platform.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The ID of organization this user belongs to on their chat platform (the Team ID in Slack), if known.
    /// </summary>
    public string? PlatformId { get; }

    /// <summary>
    /// Renders the user as a mention.
    /// </summary>
    /// <returns>The user mention.</returns>
    public override string ToString()
    {
        return $"<@{Id}>";
    }
}

using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Base type for <see cref="MemberJoinedChannelEvent"/> and <see cref="MemberLeftChannelEvent"/>.
/// </summary>
/// <param name="Type">The type of channel membership event.</param>
public abstract record ChannelMembershipEvent(string Type) : ChannelLifecycleEvent(Type)
{
    /// <summary>
    /// The team ID belonging to the team containing the channel that the user joined.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public string Team { get; set; } = null!;

    /// <summary>
    /// The type of the channel that the user joined.
    /// This value will be 'C' for a public channel and 'G' for a private channel or group.
    /// </summary>
    [JsonProperty("channel_type")]
    [JsonPropertyName("channel_type")]
    public string ChannelType { get; set; } = null!;
}

/// <summary>
/// The event raised in Slack when a member joins a public or private channel.
/// <see href="https://api.slack.com/events/member_joined_channel"/>.
/// </summary>
[Element("member_joined_channel")]
public record MemberJoinedChannelEvent() : ChannelMembershipEvent("member_joined_channel")
{
    /// <summary>
    /// If the user was invited, the message will include an inviter property containing the user ID of the inviting
    /// user. The property will be absent when a user manually joins a channel, or a user is added by default
    /// (e.g. #general channel). Also, the property is not available when a channel is converted from a public to
    /// private, where the channel history is not shared with the user.
    /// </summary>
    [JsonProperty("inviter")]
    [JsonPropertyName("inviter")]
    public string? Inviter { get; init; }
};

/// <summary>
/// The event raised in Slack when a member leaves a public or private channel.
/// <see href="https://api.slack.com/events/member_left_channel"/>.
/// </summary>
[Element("member_left_channel")]
public record MemberLeftChannelEvent() : ChannelMembershipEvent("member_left_channel");

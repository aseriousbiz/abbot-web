using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Where it all happened — the user inciting this action clicked a button on a message contained within a channel,
/// and this hash presents attributed about that channel.
/// </summary>
public record ChannelInfo
{
    /// <summary>
    /// A string identifier for the channel housing the originating message.
    /// Channel IDs are unique to the workspace they appear within.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    /// <summary>
    /// The name of the channel the message appeared in, without the leading # character.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;
}

/// <summary>
/// Information about a created channel.
/// </summary>
public record ChannelCreatedInfo : ChannelInfo
{
    /// <summary>
    /// Timestamp when the room was created.
    /// </summary>
    [JsonProperty("created")]
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>
    /// The ID of the user that created the room.
    /// </summary>
    [JsonProperty("creator")]
    [JsonPropertyName("creator")]
    public string Creator { get; init; } = null!;
}

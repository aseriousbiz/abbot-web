using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// The event raised in Slack when a channel is renamed.
/// <see href="https://api.slack.com/events/channel_rename"/>.
/// </summary>
/// <remarks>
/// This event is unique because <code>channel</code> is an object and not a string, hence this
/// doesn't inherit from <see cref="ChannelLifecycleEvent"/>.
/// </remarks>
[Element("channel_rename")]
public record ChannelRenameEvent() : EventBody("channel_rename")
{
    /// <summary>
    /// Information about the renamed channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public ChannelInfo Channel { get; set; } = null!;
}

/// <summary>
/// Event raised when a channel is created.
/// </summary>
public record ChannelCreatedEvent() : EventBody("channel_created")
{
    /// <summary>
    /// Information about the created channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public ChannelCreatedInfo Channel { get; set; } = null!;
}

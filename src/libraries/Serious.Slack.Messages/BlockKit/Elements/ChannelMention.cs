using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// A mention of a channel in the contents of an incoming message.
/// </summary>
[Element("channel")]
public sealed record ChannelMention() : StyledElement("channel")
{
    /// <summary>
    /// The Slack channel Id.
    /// </summary>
    [JsonProperty("channel_id")]
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; } = null!;
}

using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// A mention of a user in the contents of an incoming message.
/// </summary>
[Element("user")]
public record UserMention() : StyledElement("user")
{
    /// <summary>
    /// The Id of the mentioned user.
    /// </summary>
    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = null!;
}

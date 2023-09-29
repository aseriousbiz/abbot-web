using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack;

/// <summary>
/// When creating a message, this is body of the message that was created.
/// </summary>
public record MessageBody : SlackMessage
{
    /// <summary>
    /// The Id of the channel the message is in.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; init; } = null!;
}

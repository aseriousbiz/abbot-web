using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// An emoji used in an incoming message.
/// </summary>
[Element("emoji")]
public record Emoji() : Element("emoji")
{
    /// <summary>
    /// Then name of the emoji.
    /// </summary>
    [JsonProperty("emoji")]
    [JsonPropertyName("emoji")]
    public string Name { get; init; } = null!;
}

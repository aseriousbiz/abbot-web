using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Payloads;

/// <summary>
/// Style that may be applied to incoming parts of a message.
/// </summary>
public record TextStyle
{
    /// <summary>
    /// If <c>true</c>, the text is bold.
    /// </summary>
    [JsonProperty("bold")]
    [JsonPropertyName("bold")]
    public bool Bold { get; init; }

    /// <summary>
    /// If <c>true</c>, the text is italicized.
    /// </summary>
    [JsonProperty("italic")]
    [JsonPropertyName("italic")]
    public bool Italic { get; init; }

    /// <summary>
    /// If <c>true</c>, the text has a strikethrough.
    /// </summary>
    [JsonProperty("strike")]
    [JsonPropertyName("strike")]
    public bool Strike { get; init; }

    /// <summary>
    /// If <c>true</c>, the text is formatted as code.
    /// </summary>
    [JsonProperty("code")]
    [JsonPropertyName("code")]
    public bool Code { get; init; }
}

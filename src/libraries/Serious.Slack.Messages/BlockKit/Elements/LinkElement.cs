using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// A link to an internal or external resource.
/// </summary>
[Element("link")]
public record LinkElement() : TextElement("link")
{
    /// <summary>
    /// The Url the link points to.
    /// </summary>
    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string Url { get; init; } = null!;
}

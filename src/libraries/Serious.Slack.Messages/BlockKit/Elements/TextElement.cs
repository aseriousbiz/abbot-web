using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// Represents a section of text within an incoming Slack message.
/// </summary>
[Element("text")]
public record TextElement : StyledElement
{
    /// <summary>
    /// Constructs a <see cref="TextElement"/> with the <c>type</c> set to <c>text</c>.
    /// </summary>
    public TextElement() : base("text")
    {
    }

    /// <summary>
    /// Constructs a <see cref="TextElement"/> with the specified <c>type</c>.
    /// </summary>
    protected TextElement(string type) : base(type)
    {
    }

    /// <summary>
    /// The text of the element.
    /// </summary>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A <c>header</c> is a plain-text block that displays in a larger, bold font.
/// Use it to delineate between different groups of content in your app's surfaces.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#file"/> for more info.
/// <para>
/// Appears in surfaces: Messages
/// </para>
/// </remarks>
/// <param name="Text">
/// A <c>plain_text</c> text object (<see cref="PlainText"/>) that is the text for the block.
/// Maximum length is 150 characters.
/// </param>
[Element("header")]
public record Header(
    [property: JsonProperty("text")]
    [property: JsonPropertyName("text")]
    PlainText? Text) : LayoutBlock("header")
{
    /// <summary>
    /// Constructs a <see cref="Header"/>.
    /// </summary>
    public Header() : this((PlainText?)null)
    {
        // We need this constructor for deserialization.
    }
}

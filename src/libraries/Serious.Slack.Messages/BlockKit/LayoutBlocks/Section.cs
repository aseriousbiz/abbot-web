using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A section is one of the most flexible blocks available - it can be used as a simple text block,
/// in combination with text fields, or side-by-side with any of the available block elements.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#section" /> for more info.
/// <para>
/// Available in surfaces: Modals Messages Home tabs
/// </para>
/// </remarks>
[Element("section")]
public sealed record Section() : LayoutBlock("section")
{
    /// <summary>
    /// Max length for a field text is 2000 characters.
    /// </summary>
    public const int MaxFieldLength = 2000;

    /// <summary>
    /// Constructs a <see cref="Section"/> with the specified text.
    /// </summary>
    /// <param name="text">The section text.</param>
    public Section(MrkdwnText text) : this()
    {
        Text = text;
    }

    /// <summary>
    /// Constructs a <see cref="Section"/> with the specified text.
    /// </summary>
    /// <param name="text">The section text.</param>
    /// <param name="accessory">
    /// One of the available <see href="https://api.slack.com/reference/block-kit/block-elements">element</see>
    /// objects (<see cref="IBlockElement"/>).
    /// </param>
    public Section(MrkdwnText text, IBlockElement accessory) : this()
    {
        Text = text;
        Accessory = accessory;
    }

    /// <summary>
    /// Constructs a <see cref="Section"/> with the specified <c>plain_text</c> text.
    /// </summary>
    /// <param name="text">The section text.</param>
    public Section(PlainText text) : this()
    {
        Text = text;
    }

    /// <summary>
    /// Constructs a <see cref="Section"/> with the specified <c>plain_text</c> text.
    /// </summary>
    /// <param name="text">The section text.</param>
    /// <param name="accessory">
    /// One of the available <see href="https://api.slack.com/reference/block-kit/block-elements">element</see>
    /// objects (<see cref="IBlockElement"/>).
    /// </param>
    public Section(PlainText text, IBlockElement accessory) : this()
    {
        Text = text;
        Accessory = accessory;
    }

    /// <summary>
    /// Constructs a <see cref="Section"/> with the specified fields.
    /// </summary>
    /// <param name="fields"></param>
    public Section(params TextObject[] fields) : this()
    {
        Fields = fields;
    }

    /// <summary>
    /// The text for the block (either <see cref="PlainText"/> or <see cref="MrkdwnText"/>). Maximum length for the text
    /// in this field is 3000 characters.
    /// </summary>
    /// <remarks>
    /// This field is not required if a valid array of fields objects is provided instead.
    /// </remarks>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public TextObject? Text { get; init; }

    /// <summary>
    /// Required if no text is provided. An array of text objects (either <see cref="PlainText"/> or
    /// <see cref="MrkdwnText"/>). Any text objects included with fields will be rendered in a compact format that
    /// allows for 2 columns of side-by-side text. Maximum number of items is 10. Maximum length for the text in each
    /// item is 2000 characters.
    /// </summary>
    [JsonProperty("fields")]
    [JsonPropertyName("fields")]
    public IReadOnlyList<TextObject>? Fields { get; init; }

    /// <summary>
    /// One of the available <see href="https://api.slack.com/reference/block-kit/block-elements">element</see>
    /// objects (<see cref="IBlockElement"/>). (Optional)
    /// </summary>
    [JsonProperty("accessory")]
    [JsonPropertyName("accessory")]
    public IBlockElement? Accessory { get; init; }
}

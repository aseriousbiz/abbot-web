using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Represent any block that contains sub elements. Makes it easier to search through Block Kit messages
/// for specific elements.
/// </summary>
public interface IBlockWithElements
{
    /// <summary>
    /// The set of elements contained in this block.
    /// </summary>
    IReadOnlyList<IElement> Elements { get; }
}

/// <summary>
/// Block that contains the contents of a rich text message.
/// </summary>
[Element("rich_text")]
public record RichTextBlock() : LayoutBlock("rich_text"), IBlockWithElements
{
    /// <summary>
    /// The set of elements contained in this block.
    /// </summary>
    [JsonProperty("elements")]
    [JsonPropertyName("elements")]
    public IReadOnlyList<IElement> Elements { get; init; } = Array.Empty<Element>();
}

/// <summary>
/// A <c>rich_text_section</c> that is contained in the <c>Elements</c> within a <c>rich_text</c> block
/// (<see cref="RichTextBlock"/>).
/// </summary>
[Element("rich_text_section")]
public record RichTextSection : Element, IBlockWithElements
{
    /// <summary>
    /// Constructs a <see cref="RichTextSection"/> with the type <c>rich_text_section</c>.
    /// </summary>
    public RichTextSection() : base("rich_text_section")
    {
    }

    /// <summary>
    /// Constructs a <see cref="RichTextSection"/> with the given <c>type</c>.
    /// </summary>
    /// <param name="type">The <c>type</c> of rich text section.</param>
    protected RichTextSection(string type) : base(type) { }

    /// <summary>
    /// The set of elements within this section.
    /// </summary>
    [JsonProperty("elements")]
    [JsonPropertyName("elements")]
    public IReadOnlyList<IElement> Elements { get; init; } = Array.Empty<Element>();
}

/// <summary>
/// The <c>rich_text_list</c> element used to render a bulleted or ordered list within a <c>rich_text</c> block.
/// </summary>
[Element("rich_text_list")]
public record RichTextList() : RichTextSection("rich_text_list")
{
    /// <summary>
    /// The list style. Either <c>ordered</c> or <c>bullet</c>.
    /// </summary>
    [JsonProperty("style")]
    [JsonPropertyName("style")]
    public RichTextListStyle Style { get; init; }

    /// <summary>
    /// The amount to indent the list.
    /// </summary>
    [JsonProperty("indent")]
    [JsonPropertyName("indent")]
    public int Indent { get; init; }

    /// <summary>
    /// The width of the border to render.
    /// </summary>
    [JsonProperty("border")]
    [JsonPropertyName("border")]
    public int Border { get; init; }
}

/// <summary>
/// The bullet style for a <see cref="RichTextList"/>.
/// </summary>
public enum RichTextListStyle
{
    /// <summary>
    /// Ordered list.
    /// </summary>
    [EnumMember(Value = "ordered")]
    Ordered,

    /// <summary>
    /// Bulleted list.
    /// </summary>
    [EnumMember(Value = "bullet")]
    Bullet
}

/// <summary>
/// A <c>rich_text_quote</c> within a <c>rich_text</c> block (<see cref="RichTextBlock"/>).
/// </summary>
[Element("rich_text_quote")]
public record RichTextQuote() : RichTextSection("rich_text_quote");

/// <summary>
/// A <c>rich_text_preformatted</c> section within a <c>rich_text</c> block.
/// </summary>
[Element("rich_text_preformatted")]
public record RichTextPreformatted() : RichTextSection("rich_text_preformatted")
{
    /// <summary>
    /// The width of the border.
    /// </summary>
    [JsonProperty("border")]
    [JsonPropertyName("border")]
    public int Border { get; init; }
}

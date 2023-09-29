using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// An element to insert an image as part of a larger block of content.
/// If you want a block with only an image in it, you're looking for the <c>image</c> element (<see cref="Image" />)
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> and <see cref="Context"/>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#image"/> for more information.
/// </remarks>
/// <param name="ImageUrl">The URL of the image to be displayed.</param>
/// <param name="AltText">A plain-text summary of the image. This should not contain any markup.</param>
[Element("image")]
public sealed record ImageElement(
        [property:JsonProperty("image_url")]
        [property:JsonPropertyName("image_url")]
        string ImageUrl,
        [property:JsonProperty("alt_text")]
        [property:JsonPropertyName("alt_text")]
        string AltText)
    : Element("image"), IContextBlockElement, IBlockElement
{
    /// <summary>
    /// Constructs an <see cref="ImageElement"/>.
    /// </summary>
    public ImageElement() : this(string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// Constructs an <see cref="ImageElement"/>.
    /// </summary>
    /// <param name="imageUrl">The URL to the image to be displayed.</param>
    /// <param name="altText">A plain-text summary of the image.</param>
    public ImageElement(Uri imageUrl, string altText) : this(imageUrl.ToString(), altText)
    {
    }
}

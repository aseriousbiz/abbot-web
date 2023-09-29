using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A simple image block, designed to make those cat photos really pop.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#image"/> for more info.
/// <para>
/// Available in surfaces: Modals, Messages, Home Taps.
/// </para>
/// </remarks>
[Element("image")]
public record Image() : LayoutBlock("image")
{
    /// <summary>
    /// The max length for the <see cref="ImageUrl"/> property value.
    /// </summary>
    public const int ImageUrlMaxLength = 3000;

    /// <summary>
    /// The max length for the <see cref="AltText"/> property value.
    /// </summary>
    public const int AltTextMaxLength = 2000;

    /// <summary>
    /// The max length for the <see cref="Title"/> property value.
    /// </summary>
    public const int TitleMaxLength = 2000;

    /// <summary>
    /// Max upload size for a single file.
    /// </summary>
    public const long MaxUploadSize = 1000L * 1000L * 1000L; // 1GB (not to be confused with GiB 1024*1024*1024)

    /// <summary>
    /// Constructs an <see cref="Image"/> with the specified image URL.
    /// </summary>
    /// <param name="imageUrl">The URL to the image.</param>
    /// <param name="altText">A plain-text summary of the image. This should not contain any markup.</param>
    /// <param name="title">The title for the image.</param>
    public Image(string imageUrl, string? altText = null, PlainText? title = null) : this(new Uri(imageUrl), altText, title)
    {
    }

    /// <summary>
    /// Constructs an <see cref="Image"/> with the specified image URL.
    /// </summary>
    /// <param name="imageUrl">The URL to the image.</param>
    /// <param name="altText">A plain-text summary of the image. This should not contain any markup.</param>
    /// <param name="title">The title for the image.</param>
    public Image(Uri imageUrl, string? altText = null, PlainText? title = null) : this()
    {
        ImageUrl = imageUrl;
        AltText = altText;
        Title = title;
    }

    /// <summary>
    /// The URL of the image to be displayed.
    /// </summary>
    [JsonProperty("image_url")]
    [JsonPropertyName("image_url")]
    public Uri ImageUrl { get; init; } = null!;

    /// <summary>
    /// A plain-text summary of the image. This should not contain any markup.
    /// </summary>
    [JsonProperty("alt_text")]
    [JsonPropertyName("alt_text")]
    public string? AltText { get; init; }

    /// <summary>
    /// An optional title for the image in the form of a text object that can only be of type: plain_text.
    /// Maximum length for the text in this field is 2000 characters.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public PlainText? Title { get; init; }
}

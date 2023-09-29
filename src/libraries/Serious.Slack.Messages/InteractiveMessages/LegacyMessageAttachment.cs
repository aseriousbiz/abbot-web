using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// An attachment to attach to a Slack message. See <see href="https://api.slack.com/reference/messaging/attachments"/>
/// for more details. One of <see cref="Fallback"/> or <see cref="Text"/> is required.
/// </summary>
/// <remarks>
/// Attachments are a legacy feature of Slack. If you are using attachments, Slack still recommends that you use the
/// <see cref="MessageAttachment.Blocks"/> property to structure and layout the content within them using Block Kit.
/// In that case, use <see cref="MessageAttachment"/> instead of this types.
/// </remarks>
public class LegacyMessageAttachment : MessageAttachment
{
    /// <summary>
    /// An array of <see href="https://api.slack.com/reference/messaging/attachments#field_objects">field objects</see>
    /// that get displayed in a table-like way. For best results, include no more than 2-3 field objects.
    /// </summary>
    [JsonProperty("fields")]
    [JsonPropertyName("fields")]
    public IList<AttachmentField> Fields { get; set; } = new List<AttachmentField>();

    /// <summary>
    /// Large title text near the top of the attachment.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// A valid URL that turns the <see cref="Title"/> text into a hyperlink.
    /// </summary>
    [JsonProperty("title_link")]
    [JsonPropertyName("title_link")]
    public string? TitleLink { get; init; }

    /// <summary>
    /// An array of field names that should be formatted by mrkdwn syntax.
    /// </summary>
    [JsonProperty("mrkdwn_in")]
    [JsonPropertyName("mrkdwn_in")]
    public IList<string> MrkDwnIn { get; init; } = new List<string>();

    /// <summary>
    /// The main body text of the attachment. It can be formatted as plain text, or with <c>mrkdwn</c> by including it
    /// in the <see cref="MrkDwnIn"/> field. The content will automatically collapse if it contains 700+ characters
    /// or 5+ line breaks, and will display a "Show more..." link to expand the content.
    /// </summary>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Text that appears above the message attachment block. It can be formatted as plain text, or with <c>mrkdwn</c>
    /// by including it in the <c>mrkdwn_in</c> field via the <see cref="MrkDwnIn"/>.
    /// </summary>
    [JsonProperty("pretext")]
    [JsonPropertyName("pretext")]
    public string? PreText { get; init; }

    /// <summary>
    /// A plain text summary of the attachment used in clients that don't show formatted text
    /// (eg. IRC, mobile notifications).
    /// </summary>
    [JsonProperty("fallback")]
    [JsonPropertyName("fallback")]
    public string Fallback { get; init; } = null!;

    /// <summary>
    /// Unique identifier for the collection of buttons within the attachment. It will be sent back to your
    /// message button action URL with each invoked action. This field is required when the attachment
    /// contains message buttons. It is key to identifying the interaction you're working with. (Required)
    /// </summary>
    [JsonProperty("callback_id")]
    [JsonPropertyName("callback_id")]
    public string CallbackId { get; set; } = null!;

    /// <summary>
    /// The color to use for the attachment sidebar in hex (including the leading #).
    /// </summary>
    [JsonProperty("color")]
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Small text used to display the author's name.
    /// </summary>
    [JsonProperty("author_name")]
    [JsonPropertyName("author_name")]
    public string? AuthorName { get; init; }

    /// <summary>
    /// A valid URL that will hyperlink the <c>author_name</c> text. Will only work if
    /// <see cref="AuthorName"/> is present.
    /// </summary>
    [JsonProperty("author_link")]
    [JsonPropertyName("author_link")]
    public Uri? AuthorLink { get; init; }

    /// <summary>
    /// A valid URL that displays a small 16px by 16px image to the left of the <c>author_name</c> text. Will only
    /// work if <see cref="AuthorName"/> is present.
    /// </summary>
    [JsonProperty("author_icon")]
    [JsonPropertyName("author_icon")]
    public Uri? AuthorIcon { get; set; }

    /// <summary>
    /// Even for message menus, remains the default value <c>default</c>.
    /// </summary>
    [JsonProperty("attachment_type")]
    [JsonPropertyName("attachment_type")]
    public string AttachmentType { get; init; } = "default";

    /// <summary>
    /// The set of actions in the attachment.
    /// </summary>
    [JsonProperty("actions")]
    [JsonPropertyName("actions")]
    public IReadOnlyList<AttachmentAction> Actions { get; set; } = Array.Empty<AttachmentAction>();

    /// <summary>
    /// A valid URL to an image file that will be displayed at the bottom of the attachment.
    /// We support GIF, JPEG, PNG, and BMP formats.
    /// </summary>
    [JsonProperty("image_url")]
    [JsonPropertyName("image_url")]
    public Uri? ImageUrl { get; init; }

    /// <summary>
    /// If the attachment is an image, this is the width of the image.
    /// </summary>
    [JsonProperty("image_width")]
    [JsonPropertyName("image_width")]
    public int? ImageWidth { get; init; }

    /// <summary>
    /// If the attachment is an image, this is the height of the image.
    /// </summary>
    [JsonProperty("image_height")]
    [JsonPropertyName("image_height")]
    public int? ImageHeight { get; init; }

    /// <summary>
    /// If the attachment is an image, this is the height of the image.
    /// </summary>
    [JsonProperty("image_bytes")]
    [JsonPropertyName("image_bytes")]
    public long? ImageBytes { get; init; }

    /// <summary>
    /// A valid URL to an image file that will be displayed as a thumbnail on the right side of a message attachment.
    /// Slack currently supports the following formats: GIF, JPEG, PNG, and BMP.
    /// The thumbnail's longest dimension will be scaled down to 75px while maintaining the aspect ratio of the image.
    /// The file size of the image must also be less than 500 KB. For best results, please use images that are already
    /// 75px by 75px.
    /// </summary>
    [JsonProperty("thumb_url")]
    [JsonPropertyName("thumb_url")]
    public Uri? ThumbUrl { get; init; }

    /// <summary>
    /// Some brief text to help contextualize and identify an attachment. Limited to 300 characters, and may
    /// be truncated further when displayed to users in environments with limited screen real estate.
    /// </summary>
    [JsonProperty("footer")]
    [JsonPropertyName("footer")]
    public string? Footer { get; init; }

    /// <summary>
    /// A valid URL to an image file that will be displayed beside the footer text. Will only work if author_name is
    /// present. We'll render what you provide at 16px by 16px. It's best to use an image that is similarly sized.
    /// </summary>
    [JsonProperty("footer_icon")]
    [JsonPropertyName("footer_icon")]
    public Uri? FooterIcon { get; init; }

    /// <summary>
    /// An integer Unix timestamp that is used to related your attachment to a specific time. The attachment will
    /// display the additional timestamp value as part of the attachment's footer.
    /// </summary>
    [JsonProperty("ts")]
    [JsonPropertyName("ts")]
    public string? Timestamp { get; init; }
}

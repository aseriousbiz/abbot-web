using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// Fields get displayed in a table-like way.
/// </summary>
public class AttachmentField
{
    /// <summary>
    /// Shown as a bold heading displayed in the field object. It cannot contain markup and will be escaped for you.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The text value displayed in the field object. It can be formatted as plain text, or with <c>mrkdwn</c> by
    /// using the <c>mrkdwn_in</c> option in the <see cref="LegacyMessageAttachment.MrkDwnIn"/> property.
    /// </summary>
    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>
    /// Indicates whether the field object is short enough to be displayed side-by-side with other field objects.
    /// Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("short")]
    [JsonPropertyName("short")]
    public bool IsShortEnoughToDisplaySideBySide { get; init; }
}

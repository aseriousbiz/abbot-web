using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// Displays a remote file. You can't add this block to app surfaces directly, but it will show up when
/// retrieving messages that contain remote files.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#file"/> for more info.
/// <para>
/// Appears in surfaces: Messages
/// </para>
/// </remarks>
[Element("file")]
public record FileBlock() : LayoutBlock("file")
{
    /// <summary>
    /// The external unique ID for this file.
    /// </summary>
    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string ExternalId { get; init; } = null!;

    /// <summary>
    /// At the moment, source will always be remote for a remote file.
    /// </summary>
    [JsonProperty("source")]
    [JsonPropertyName("source")]
    public string Source { get; init; } = null!;
}

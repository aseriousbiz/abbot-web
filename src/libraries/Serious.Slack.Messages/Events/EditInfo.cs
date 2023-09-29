using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Events;

/// <summary>
/// When a message is edited, this contains information about who and when the edit occurred.
/// </summary>
public class EditInfo
{
    /// <summary>
    /// The Id of the user that edited the message.
    /// </summary>
    public string User { get; init; } = null!;

    /// <summary>
    /// The unique (per-channel) timestamp for the edit.
    /// </summary>
    [JsonProperty("ts")]
    [JsonPropertyName("ts")]
    public string Timestamp { get; init; } = null!;
}

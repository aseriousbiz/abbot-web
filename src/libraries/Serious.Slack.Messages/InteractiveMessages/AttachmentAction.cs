using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// A menu or button to attach to a message.
/// </summary>
public class AttachmentAction : PayloadAction
{
    /// <summary>
    /// The Id of the action.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    /// <summary>
    /// The user-facing label for the message button or menu representing this action. Cannot contain markup.
    /// Best to keep these short and decisive. Use a maximum of 30 characters or so for best results across form
    /// factors.
    /// </summary>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string Text { get; init; } = null!;

    /// <summary>
    /// If you provide a JSON hash of confirmation fields, your button or menu will pop up a dialog with your
    /// indicated text and choices, giving them one last chance to avoid a destructive action or other undesired
    /// outcome.
    /// </summary>
    [JsonProperty("confirm")]
    [JsonPropertyName("confirm")]
    public ConfirmationDialog? Confirm { get; init; }

    /// <summary>
    /// 
    /// </summary>
    [JsonProperty("style")]
    [JsonPropertyName("style")]
    public string? Style { get; init; }
}

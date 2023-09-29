using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// Protect users from destructive actions or particularly distinguished decisions by asking them to confirm
/// their button click one more time. Use confirmation dialogs with care.
/// </summary>
public class ConfirmationDialog
{
    /// <summary>
    /// Title the pop up window. Please be brief.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Describe in detail the consequences of the action and contextualize your button text choices. Use a maximum of
    /// 30 characters or so for best results across form factors.
    /// </summary>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string Text { get; init; } = null!;

    /// <summary>
    /// The text label for the button to continue with an action. Keep it short. Defaults to <c>Okay</c>.
    /// </summary>
    [JsonProperty("ok_text")]
    [JsonPropertyName("ok_text")]
    public string OkText { get; init; } = null!;

    /// <summary>
    /// The text label for the button to cancel the action. Keep it short. Defaults to <c>Cancel</c>.
    /// </summary>
    [JsonProperty("dismiss_text")]
    [JsonPropertyName("dismiss_text")]
    public string DismissText { get; init; } = null!;
}

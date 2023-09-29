using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;

namespace Serious.Slack.BlockKit;

/// <summary>
/// An object that defines a dialog that provides a confirmation step to any interactive element.
/// This dialog will ask the user to confirm their action by offering a confirm and deny buttons.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#confirm"/> for
/// more information.
/// </remarks>
/// <param name="Title">
/// A <c>plain_text</c> only text object that defines the dialog's title.
/// Maximum length for this field is 100 characters.
/// </param>
/// <param name="Text">
/// A text object (either <see cref="PlainText"/> or <see cref="MrkdwnText"/>) that defines the explanatory text
/// that appears in the confirm dialog. Maximum length for this field is 300 characters.
/// </param>
/// <param name="Confirm">
/// A <c>plain_text</c> only text object that defines the text of the button
/// that confirms the action. Maximum length for this field is 30 characters.
/// </param>
/// <param name="Deny">
/// A <c>plain_text</c> only text object that defines the text of the button
/// that cancels the action. Maximum length for this field is 30 characters.
/// </param>
public record ConfirmationDialog(
    [property:JsonProperty("title")]
    [property:JsonPropertyName("title")]
    PlainText Title,

    [property:JsonProperty("text")]
    [property:JsonPropertyName("text")]
    TextObject Text,

    [property:JsonProperty("confirm")]
    [property:JsonPropertyName("confirm")]
    PlainText Confirm,

    [property:JsonProperty("deny")]
    [property:JsonPropertyName("deny")]
    PlainText Deny)
{
    /// <summary>
    /// Constructs a new <see cref="ConfirmationDialog"/> with some default values.
    /// </summary>
    public ConfirmationDialog() : this(
        "Confirm",
        new MrkdwnText("Are you sure?"),
        "Yes",
        "No")
    {
    }

    ButtonStyle? _style;

    /// <summary>
    /// Defines the color scheme applied to the <c>confirm</c> button. A value of <c>danger</c>
    /// will display the button with a red background on desktop, or red text on mobile. A value
    /// of <c>primary</c> will display the button with a green background on desktop, or blue
    /// text on mobile. If this field is not provided, the default value will be <c>primary</c>.
    /// </summary>
    [JsonProperty("style")]
    [JsonPropertyName("style")]
    public ButtonStyle? Style
    {
        get => _style;
        init => _style = value is ButtonStyle.Default
            ? null // "default" is not valid for Block Kit.
            : value;
    }
}

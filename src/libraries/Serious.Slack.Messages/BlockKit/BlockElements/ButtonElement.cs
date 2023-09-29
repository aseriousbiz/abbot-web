using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// An interactive component that inserts a button. The button can be a trigger for
/// anything from opening a simple link to starting a complex workflow.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#button"/> for more info.
/// <para>
/// Works with block types: <see cref="Section"/> and <see cref="Actions"/>.
/// </para>
/// </remarks>
/// <param name="Text">
/// The button's <c>plain_text</c> text. Text may be truncated with ~30 characters. Maximum length for the text in this
/// field is 75 characters.
/// </param>
/// <param name="Value">
/// The value to send along with the interaction payload. Maximum length for this field is 2000 characters.
/// </param>
/// <param name="Url">
/// A URL to load in the user's browser when the button is clicked. Maximum length for this field is 3000
/// characters. If you're using url, you'll still receive an interaction payload and will need to send an
/// acknowledgement response.
/// </param>
/// <param name="Confirm">
/// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
/// dialog after the button is clicked.
/// </param>
[Element("button")]
public sealed record ButtonElement(
    [property:JsonProperty("text")]
    [property:JsonPropertyName("text")]
    PlainText Text,

    [property:JsonProperty("value")]
    [property:JsonPropertyName("value")]
    string? Value = null,

    [property:JsonProperty("url")]
    [property:JsonPropertyName("url")]
    Uri? Url = null,

    [property:JsonProperty("confirm")]
    [property:JsonPropertyName("confirm")]
    ConfirmationDialog? Confirm = null
    ) : InteractiveElement("button"), IValueElement, IActionElement
{
    /// <summary>
    /// Constructs a <see cref="ButtonElement"/>.
    /// </summary>
    public ButtonElement() : this(string.Empty)
    {
    }

    readonly ButtonStyle? _style;

    /// <summary>
    /// Decorates buttons with alternative visual color schemes. Use this option with restraint.
    /// </summary>
    [property: JsonProperty("style")]
    [property: JsonPropertyName("style")]
    public ButtonStyle? Style
    {
        get => _style;
        init => _style = value is ButtonStyle.Default
            ? null // "default" is not valid for Block Kit.
            : value;
    }
}

/// <summary>
/// The set of styles available for a button.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ButtonStyle
{
    /// <summary>
    /// The <c>default</c> style.
    /// </summary>
    [EnumMember(Value = null)]
    Default,

    /// <summary>
    /// <c>primary</c> is used to denote the primary button.
    /// </summary>
    [EnumMember(Value = "primary")]
    Primary,

    /// <summary>
    /// <c>danger</c> is used to denote a dangerous action.
    /// </summary>
    [EnumMember(Value = "danger")]
    Danger
}

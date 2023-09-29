using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;
using Serious.Slack.Payloads;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A plain-text input, similar to the HTML &lt;input&gt; tag, creates a field where a user
/// can enter freeform data. It can appear as a single-line field or a larger textarea
/// using the multiline flag.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#input"/> for more
/// information.
/// <para>
/// Plain-text input elements are supported in the following
/// <see href="https://api.slack.com/surfaces">app surfaces</see>:
/// Home tabs Messages Modals
/// </para>
/// </remarks>
[Element("plain_text_input")]
public sealed record PlainTextInput() : InteractiveElement("plain_text_input"), IValueElement, IInputElement
{
    /// <summary>
    /// Constructs a plain-text input element.
    /// </summary>
    /// <param name="actionId">Identifies this input element.</param>
    public PlainTextInput(string actionId) : this()
    {
        ActionId = actionId;
    }

    /// <summary>
    /// Constructs a plain-text input element.
    /// </summary>
    /// <param name="multiline">Whether the input should be multi-line or not.</param>
    /// <param name="actionId">Identifies this input element.</param>
    public PlainTextInput(bool multiline, string actionId) : this(actionId)
    {
        Multiline = multiline;
    }

    /// <summary>
    /// A <c>plain_text</c> only text object that defines the placeholder text shown in the <c>plain_text_input</c>.
    /// Maximum length for this field is 150 characters.
    /// </summary>
    [JsonProperty("placeholder")]
    [JsonPropertyName("placeholder")]
    public PlainText? Placeholder { get; init; }

    /// <summary>
    /// The initial value in the plain-text input when it is loaded.
    /// </summary>
    [JsonProperty("initial_value")]
    [JsonPropertyName("initial_value")]
    public string? InitialValue { get; init; }

    /// <summary>
    /// The value of the plain-text input when it is dispatched.
    /// </summary>
    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>
    /// Indicates whether the input will be a single line (<c>false</c>) or a larger
    /// textarea (<c>true</c>). Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("multiline")]
    [JsonPropertyName("multiline")]
    public bool Multiline { get; init; }

    /// <summary>
    /// The minimum length of input that the user must provide.
    /// If the user provides less, they will receive an error. Maximum value is 3000.
    /// </summary>
    [JsonProperty("min_length")]
    [JsonPropertyName("min_length")]
    public int MinLength { get; init; }

    /// <summary>
    /// The maximum length of input that the user can provide.
    /// If the user provides more, they will receive an error.
    /// </summary>
    [JsonProperty("max_length")]
    [JsonPropertyName("max_length")]
    public int MaxLength { get; init; }

    /// <summary>
    /// A <see cref="DispatchConfiguration"/> that determines when during text
    /// input the element returns a <see cref="BlockActionsPayload"/>.
    /// </summary>
    [JsonProperty("dispatch_action_config")]
    [JsonPropertyName("dispatch_action_config")]
    public DispatchConfiguration? DispatchConfiguration { get; init; }

    /// <summary>
    /// Indicates whether the element will be set to auto focus within the view object.
    /// Only one element can be set to <c>true</c>. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("focus_on_load")]
    [JsonPropertyName("focus_on_load")]
    public bool FocusOnLoad { get; init; }
}

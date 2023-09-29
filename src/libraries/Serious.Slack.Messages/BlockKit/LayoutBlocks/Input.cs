using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A block that collects information from users. Set the <c>element</c> (<see cref="Element"/>) property to the
/// input element.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#input"/> for more info.
/// <para>
/// Available in surfaces: Modals Messages Home tabs
/// </para>
/// </remarks>
[Element("input")]
// We're unlikely to use this in the same place as System.Windows.Input.
#pragma warning disable CA1724
public record Input() : LayoutBlock("input")
#pragma warning restore
{
    /// <summary>
    /// Constructs an <see cref="Input"/>.
    /// </summary>
    /// <param name="label">The label for the input.</param>
    /// <param name="inputElement">The input element.</param>
    /// <param name="blockId">The BlockId for the input.</param>
    /// <param name="dispatchAction">The <see cref="DispatchAction"/> for the input.</param>
    public Input(PlainText label, IInputElement inputElement, string? blockId = null, bool dispatchAction = false) : this()
    {
        Label = label;
        Element = inputElement;
        BlockId = blockId;
        DispatchAction = dispatchAction;
    }

    /// <summary>
    /// A <c>plain_text</c> text object (<see cref="PlainText"/>) that defines the label shown above
    /// this group of options. Maximum length for this field is 2000 characters.
    /// </summary>
    [JsonProperty("label")]
    [JsonPropertyName("label")]
    public PlainText Label { get; init; } = null!;

    /// <summary>
    /// An input element that collects information from users such as <c>plain_text_input</c>
    /// (see <see cref="PlainTextInput"/>), <c>checkboxes</c> (see <see cref="CheckboxGroup"/>), <c>radio_buttons</c>
    /// (see <see cref="RadioButtonGroup"/>), a select menu element (classes that derive from <see cref="SelectMenu"/>),
    /// a multi-select menu (classes that derive from <see cref="MultiSelectMenu"/>), or a datepicker
    /// (<see cref="DatePicker"/>).
    /// </summary>
    [JsonProperty("element")]
    [JsonPropertyName("element")]
    public IInputElement Element { get; init; } = null!;

    /// <summary>
    /// A boolean that indicates whether or not the use of elements in this block should dispatch a <c>block_actions</c>
    /// payload (<see cref="IMessageBlockActionsPayload"/> or <see cref="IViewBlockActionsPayload"/>). Defaults to
    /// <c>false</c>.
    /// </summary>
    [JsonProperty("dispatch_action")]
    [JsonPropertyName("dispatch_action")]
    public bool DispatchAction { get; init; }

    /// <summary>
    /// An optional <c>plain_text</c> hint that appears below an input element in a lighter grey. Maximum length for
    /// the text in this field is 2000 characters.
    /// </summary>
    [JsonProperty("hint")]
    [JsonPropertyName("hint")]
    public PlainText? Hint { get; init; }

    /// <summary>
    /// A boolean that indicates whether the input element may be empty when a user submits the modal. Defaults to
    /// <c>false</c>.
    /// </summary>
    [JsonProperty("optional")]
    [JsonPropertyName("optional")]
    public bool Optional { get; init; }
}

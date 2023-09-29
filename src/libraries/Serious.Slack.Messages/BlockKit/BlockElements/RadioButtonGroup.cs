using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A radio button group that allows a user to choose one item from a list of possible options.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> <see cref="Actions"/> <see cref="Input"/>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#radio"/> for more
/// information.
/// <para>
/// Radio buttons are only supported in the following app surfaces: Home tabs Modals Messages
/// </para>
/// </remarks>
[Element("radio_buttons")]
public sealed record RadioButtonGroup() : InteractiveElement("radio_buttons"), IActionElement, IInputElement, IValueElement
{
    /// <summary>
    /// Constructs a <see cref="RadioButtonGroup"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to include in the menu.</param>
    public RadioButtonGroup(IEnumerable<CheckOption> options) : this()
    {
        Options = options.ToList();
    }

    /// <summary>
    /// Constructs a <see cref="RadioButtonGroup"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to include in the menu.</param>
    public RadioButtonGroup(params CheckOption[] options) : this()
    {
        Options = options;
    }

    /// <summary>
    /// An array of <see cref="CheckOption"/> instances that belong to this specific group.
    /// Maximum of 10 items.
    /// </summary>
    [JsonProperty("options")]
    [JsonPropertyName("options")]
    public IReadOnlyList<CheckOption> Options { get; init; } = Array.Empty<CheckOption>();

    /// <summary>
    /// An array of <see cref="CheckOption"/> instances that belong to this specific group.
    /// Maximum of 10 items.
    /// </summary>
    [JsonProperty("selected_option")]
    [JsonPropertyName("selected_option")]
    public CheckOption? SelectedOption { get; init; }

    /// <summary>
    /// A single <see cref="CheckOption"/> that exactly one of the options within
    /// <see cref="Options"/>. This option will be selected when the radio button
    /// group initially loads.
    /// </summary>
    [JsonProperty("initial_option")]
    [JsonPropertyName("initial_option")]
    public CheckOption? InitialOption { get; init; }

    /// <summary>
    /// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
    /// dialog that appears after clicking one of the radio buttons in this element.
    /// </summary>
    [JsonProperty("confirm")]
    [JsonPropertyName("confirm")]
    public ConfirmationDialog? Confirm { get; init; }

    /// <summary>
    /// Indicates whether the element will be set to auto focus within the view object.
    /// Only one element can be set to <c>true</c>. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("focus_on_load")]
    [JsonPropertyName("focus_on_load")]
    public bool FocusOnLoad { get; init; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    string? IValueElement.Value
    {
        get => SelectedOption?.Value;
        init => throw new InvalidOperationException();
    }
}

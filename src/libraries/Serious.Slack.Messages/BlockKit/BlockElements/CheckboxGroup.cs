using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A checkbox group that allows a user to choose multiple items from a list of possible options.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#checkboxes"/> for more
/// information.
/// <para>
/// Works with block types: <see cref="Section"/> <see cref="Actions"/> <see cref="Input"/>
/// </para>
/// <para>
/// Checkboxes are only supported in the following app surfaces: Home tabs Modals Messages
/// </para>
/// </remarks>
[Element("checkboxes")]
public sealed record CheckboxGroup() : InteractiveElement("checkboxes"), IMultiValueElement, IInputElement, IActionElement
{
    /// <summary>
    /// Constructs a <see cref="CheckboxGroup"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to include in the menu.</param>
    public CheckboxGroup(params CheckOption[] options) : this()
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
    /// An array of the selected <see cref="CheckOption"/> instances.
    /// </summary>
    [JsonProperty("selected_options")]
    [JsonPropertyName("selected_options")]
    public IReadOnlyList<CheckOption> SelectedOptions { get; init; } = Array.Empty<CheckOption>();

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    IReadOnlyList<string> IMultiValueElement.Values
    {
        get => SelectedOptions.Select(o => o.Value).ToList();
        init => throw new InvalidOperationException();
    }

    /// <summary>
    /// An array of <see cref="CheckOption"/> that exactly matches one or more of the
    /// options within <see cref="Options"/>. These options will be selected when the
    /// checkbox group initially loads.
    /// </summary>
    [JsonProperty("initial_options")]
    [JsonPropertyName("initial_options")]
    public IReadOnlyList<CheckOption> InitialOptions { get; init; } = Array.Empty<CheckOption>();

    /// <summary>
    /// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
    /// dialog that appears after clicking one of the checkboxes in this element.
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
}

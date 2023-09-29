using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// An element which lets users easily select a date from a calendar style UI.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> <see cref="Actions"/> <see cref="Input"/>
/// </remarks>
[Element("datepicker")]
public record DatePicker() : InteractiveElement("datepicker"), IActionElement, IInputElement, IValueElement
{
    /// <summary>
    /// Constructs a <see cref="DatePicker"/> with the specified action id.
    /// </summary>
    /// <param name="actionId">Identifies this input element.</param>
    public DatePicker(string actionId) : this()
    {
        ActionId = actionId;
    }

    /// <summary>
    /// A <c>plain_text</c> only text object that defines the placeholder text shown in the datepicker.
    /// Maximum length for this field is 150 characters.
    /// </summary>
    [JsonProperty("placeholder")]
    [JsonPropertyName("placeholder")]
    public PlainText? Placeholder { get; init; }

    /// <summary>
    /// The initial date that is selected when the element is loaded.
    /// This should be in the format <c>YYYY-MM-DD</c>.
    /// </summary>
    [JsonProperty("initial_date")]
    [JsonPropertyName("initial_date")]
    public string? InitialDate { get; init; }

    /// <summary>
    /// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
    /// dialog that appears after a date is selected.
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

    /// <summary>
    /// The date selected by the user in the format <c>yyyy-MM-dd</c>.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DateOnly? SelectedDate => Value is not null
        ? DateOnly.ParseExact(Value, "yyyy-MM-dd")
        : null;

    /// <summary>
    /// Retrieves the selected date as a string. Use <see cref="SelectedDate"/> to get the value a <see cref="DateOnly"/>.
    /// </summary>
    [JsonProperty("selected_date")]
    [JsonPropertyName("selected_date")]
    public string? Value { get; init; }
}

using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// An element which allows selection of a time of day.
/// <para>
/// On desktop clients, this time picker will take the form of a dropdown
/// list with free-text entry for precise choices. On mobile clients, the
/// time picker will use native time picker UIs.
/// </para>
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#timepicker" /> for more
/// information.
/// <para>
/// Works with block types: <see cref="Section"/> <see cref="Actions"/> <see cref="Input"/>
/// </para>
/// </remarks>
[Element("timepicker")]
public sealed record TimePicker() : InteractiveElement("timepicker"), IValueElement, IInputElement, IActionElement
{
    /// <summary>
    /// Constructs a <see cref="TimePicker"/> with the specified action id.
    /// </summary>
    /// <param name="actionId">Identifies this input element.</param>
    public TimePicker(string actionId) : this()
    {
        ActionId = actionId;
    }

    /// <summary>
    /// The initial time that is selected when the element is loaded. This should be
    /// in the format <c>HH:mm</c>, where <c>HH</c> is the 24-hour format of an hour
    /// (00 to 23) and <c>mm</c> is minutes with leading zeros (00 to 59), for
    /// example <c>22:25</c> for 10:25pm.
    /// </summary>
    [JsonProperty("initial_time")]
    [JsonPropertyName("initial_time")]
    public string? InitialTime { get; init; }

    /// <summary>
    /// The time the user selected. This will be in the format <c>HH:mm</c>, where <c>HH</c> is the 24-hour format of an
    /// hour (00 to 23) and <c>mm</c> is minutes with leading zeros (00 to 59), for example <c>22:25</c> for 10:25pm.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeOnly? SelectedTime => Value is not null
        ? TimeOnly.ParseExact(Value, "HH:mm")
        : null;

    /// <summary>
    /// A <c>plain_text</c> only text object that defines the placeholder text shown in the
    /// timepicker. Maximum length for this field is 150 characters.
    /// </summary>
    [JsonProperty("placeholder")]
    [JsonPropertyName("placeholder")]
    public PlainText? Placeholder { get; init; }

    /// <summary>
    /// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
    /// dialog that appears after a time is selected.
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
    /// Retrieves the selected time as a string. Use <see cref="SelectedTime"/> to get the value a <see cref="TimeOnly"/>.
    /// </summary>
    [JsonProperty("selected_time")]
    [JsonPropertyName("selected_time")]
    public string? Value { get; init; }
}

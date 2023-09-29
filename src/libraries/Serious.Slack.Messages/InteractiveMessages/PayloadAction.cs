using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// Represents a slack action in response to an interactive element. For example, this would have information about
/// the button that was clicked.
/// </summary>
public class PayloadAction
{
    /// <summary>
    /// Yes	Provide a string to give this specific action a name. The name will be returned to your Action URL along
    /// with the message's callback_id when this action is invoked. Use it to identify this particular response path.
    /// If multiple actions share the same name, only one of them can be in a triggered state.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Provide a string identifying this specific action. It will be sent to your Action URL along with the name
    /// and attachment's callback_id. If providing multiple actions with the same name, value can be strategically
    /// used to differentiate intent. Your value may contain up to 2000 characters.
    /// </summary>
    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>
    /// Provide <c>button</c> when this action is a message button or provide <c>select</c>
    /// when the action is a message menu.
    /// </summary>
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; init; } = "button";

    /// <summary>
    /// The selected option in the case of a select menu.
    /// </summary>
    [JsonProperty(PropertyName = "selected_option")]
    public SelectOption? SelectedOption { get; init; }

    /// <summary>
    /// The set of selected options in the case of a multi-select menu.
    /// </summary>
    [JsonProperty(PropertyName = "selected_options")]
    public IReadOnlyList<SelectOption> SelectedOptions { get; init; } = new List<SelectOption>();
}

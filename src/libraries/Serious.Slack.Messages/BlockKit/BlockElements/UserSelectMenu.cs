using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// This multi-select menu will populate its options with a list of Slack users visible
/// to the current user in the active workspace.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> and <see cref="Input"/>.
/// <para>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#users_select"/>
/// for more information.
/// </para>
/// </remarks>
[Element("users_select")]
public sealed record UserSelectMenu() : SingleSelectMenu("users_select"), IValueElement
{
    /// <summary>
    /// The user ID of any valid user to be pre-selected when the menu loads.
    /// </summary>
    [JsonProperty("initial_user")]
    [JsonPropertyName("initial_user")]
    public string? InitialUser { get; init; }

    /// <summary>
    /// The selected user.
    /// </summary>
    [JsonProperty("selected_user")]
    [JsonPropertyName("selected_user")]
    public override string? SelectedValue { get; init; }

    /// <summary>
    /// Provides a generic way to get the value of this element.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    string? IValueElement.Value
    {
        get => SelectedValue;
        init => SelectedValue = value;
    }
}

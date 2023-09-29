using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// This multi-select menu will populate its options with a list of public channels
/// visible to the current user in the active workspace.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> <see cref="Actions"/> <see cref="Input"/>.
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#channel_select"/>
/// </remarks>
[Element("channels_select")]
public sealed record ChannelSelectMenu() : SingleSelectMenu("channels_select"), IValueElement
{
    /// <summary>
    /// The ID of any valid public channel to be pre-selected when the menu loads.
    /// </summary>
    [JsonProperty("initial_channels")]
    [JsonPropertyName("initial_channels")]
    public string? InitialChannel { get; init; }

    /// <summary>
    /// When set to <c>true</c>, the view_submission payload from the menu's parent
    /// view will contain a <c>response_url</c>. This <c>response_url</c> can be used
    /// for message responses. The target conversation for the message will be determined
    /// by the value of this select menu.
    /// </summary>
    /// <remarks>
    /// This field only works with menus in input blocks in modals.
    /// </remarks>
    [JsonProperty("response_url_enabled")]
    [JsonPropertyName("response_url_enabled")]
    public bool ResponseUrlEnabled { get; init; }

    /// <summary>
    /// The selected channel.
    /// </summary>
    [JsonProperty("selected_channel")]
    [JsonPropertyName("selected_channel")]
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

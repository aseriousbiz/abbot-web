using System;
using System.Collections.Generic;
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
/// Works with block types: <see cref="Section"/> and <see cref="Input"/>.
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#channel_multi_select"/>
/// </remarks>
[Element("multi_channels_select")]
public sealed record ChannelsMultiSelectMenu() : MultiSelectMenu("multi_channels_select"), IMultiValueElement
{
    /// <summary>
    /// Constructs a <see cref="ChannelsMultiSelectMenu"/> with the specified action id.
    /// </summary>
    /// <param name="actionId">Identifies this input element.</param>
    public ChannelsMultiSelectMenu(string actionId) : this()
    {
        ActionId = actionId;
    }

    /// <summary>
    /// An array of one or more IDs of any valid public channel to be pre-selected
    /// when the menu loads.
    /// </summary>
    [JsonProperty("initial_channels")]
    [JsonPropertyName("initial_channels")]
    public IReadOnlyList<string>? InitialChannels { get; init; }

    /// <summary>
    /// An array of the selected public channels.
    /// </summary>
    [JsonProperty("selected_channels")]
    [JsonPropertyName("selected_channels")]
    public override IReadOnlyList<string> SelectedValues { get; init; } = Array.Empty<string>();

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    IReadOnlyList<string> IMultiValueElement.Values
    {
        get => SelectedValues;
        init => SelectedValues = value;
    }
}

using System;
using System.Collections.Generic;
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
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#users_multi_select"/>
/// for more information.
/// </para>
/// </remarks>
[Element("multi_users_select")]
public sealed record UsersMultiSelectMenu() : MultiSelectMenu("multi_users_select"), IMultiValueElement
{
    /// <summary>
    /// An array of user IDs of any valid users to be pre-selected when the menu loads.
    /// </summary>
    [JsonProperty("initial_users")]
    [JsonPropertyName("initial_users")]
    public IReadOnlyList<string> InitialUsers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// An array of the selected user Ids.
    /// </summary>
    [JsonProperty("selected_users")]
    [JsonPropertyName("selected_users")]
    public override IReadOnlyList<string> SelectedValues { get; init; } = Array.Empty<string>();

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    IReadOnlyList<string> IMultiValueElement.Values
    {
        get => SelectedValues;
        init => SelectedValues = value;
    }
}

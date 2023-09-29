using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Base class for A multi-select menu. Allows a user to select multiple items from a list
/// of options. Just like regular select menus, multi-select menus also include type-ahead
/// functionality, where a user can type a part or all of an option string to filter the list.
/// </summary>
public abstract record MultiSelectMenu(string Type) : SelectMenu(Type)
{
    /// <summary>
    /// Specifies the maximum number of items that can be selected in the menu.
    /// Minimum number is 1.
    /// </summary>
    [JsonProperty("max_selected_items")]
    [JsonPropertyName("max_selected_items")]
    public int MaxSelectedItems { get; init; }

    /// <summary>
    /// Indicates whether the element will be set to auto focus within the view object.
    /// Only one element can be set to <c>true</c>. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("focus_on_load")]
    [JsonPropertyName("focus_on_load")]
    public bool FocusOnLoad { get; init; }

    /// <summary>
    /// The selected conversations.
    /// </summary>
    public abstract IReadOnlyList<string> SelectedValues { get; init; }
}

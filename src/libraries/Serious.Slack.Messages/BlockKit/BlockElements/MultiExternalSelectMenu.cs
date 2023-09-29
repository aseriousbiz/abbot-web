using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;


namespace Serious.Slack.BlockKit;

/// <summary>
/// This menu will load its options from an external data source, allowing for a dynamic list of options.
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#external_multi_select"/> for details on
/// how to set it up.
/// </summary>
[Element("multi_external_select")]
public sealed record MultiExternalSelectMenu() : SelectMenu("multi_external_select"), IMultiValueElement
{
    /// <summary>
    /// When the typeahead field is used, a request will be sent on every character change. If you prefer fewer
    /// requests or more fully ideated queries, use this property to tell Slack the fewest number of typed characters
    /// required before dispatch. The default value is <c>3</c>.
    /// </summary>
    [JsonProperty("min_query_length")]
    [JsonPropertyName("min_query_length")]
    public int MinQueryLength { get; init; } = 3;

    /// <summary>
    /// An array of <see cref="Option"/> instances that exactly match one or more of the
    /// options loaded from the external URL.
    /// </summary>
    [JsonProperty("selected_options")]
    [JsonPropertyName("selected_options")]
    public IReadOnlyList<Option> SelectedOptions { get; init; } = Array.Empty<Option>();

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    IReadOnlyList<string> IMultiValueElement.Values
    {
        get => SelectedOptions.Select(o => o.Value).ToList();
        init => throw new InvalidOperationException();
    }

    /// <summary>
    /// An array of <see cref="Option"/> instances that exactly match one or more of the
    /// options loaded from the external URL.
    /// </summary>
    [JsonProperty("initial_options")]
    [JsonPropertyName("initial_options")]
    public IReadOnlyList<Option>? InitialOptions { get; init; }

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
}

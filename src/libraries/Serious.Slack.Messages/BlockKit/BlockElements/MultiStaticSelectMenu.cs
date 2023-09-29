using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// This is the simplest form of select menu, with a static list of options passed in when defining the element.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> and <see cref="Input"/>.
/// <para>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#static_multi_select"/>
/// for more information.
/// </para>
/// </remarks>
[Element("multi_static_select")]
public sealed record MultiStaticSelectMenu() : SelectMenu("multi_static_select"), IMultiValueElement
{
    /// <summary>
    /// Constructs a <see cref="MultiStaticSelectMenu"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to include in the menu.</param>
    public MultiStaticSelectMenu(params Option[] options) : this()
    {
        Options = options;
    }

    /// <summary>
    /// Constructs a <see cref="MultiStaticSelectMenu"/> with the specified option groups.
    /// </summary>
    /// <param name="optionGroups">The options to include in the menu.</param>
    public MultiStaticSelectMenu(params OptionGroup[] optionGroups) : this()
    {
        OptionGroups = optionGroups;
    }

    /// <summary>
    /// An array of <see cref="Option"/> instances. Maximum of 100 items.
    /// If <see cref="OptionGroups"/> is specified, this field should not be.
    /// </summary>
    [JsonProperty("options")]
    [JsonPropertyName("options")]
    public IReadOnlyList<Option>? Options { get; init; }

    /// <summary>
    /// An array of <see cref="OptionGroup"/> instances. Maximum of 100 items.
    /// If <see cref="Options"/> is specified, this should not be.
    /// </summary>
    [JsonProperty("option_groups")]
    [JsonPropertyName("option_groups")]
    public IReadOnlyList<OptionGroup>? OptionGroups { get; init; }

    /// <summary>
    /// An array of <see cref="Option"/> instances that exactly match one or more of the
    /// options within <see cref="Options"/> or <see cref="OptionGroups"/>. These options will
    /// be selected when the menu initially loads.
    /// </summary>
    [JsonProperty("initial_options")]
    [JsonPropertyName("initial_options")]
    public IReadOnlyList<Option>? InitialOptions { get; init; }

    /// <summary>
    /// An array of <see cref="Option"/> instances that exactly match one or more of the
    /// options within <see cref="Options"/> or <see cref="OptionGroups"/>. These options will
    /// be selected when the menu initially loads.
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// This is the simplest form of select menu, with a static list of options
/// passed in when defining the element.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> <see cref="Actions" /> <see cref="Input"/>.
/// <para>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#static_select"/>
/// for more information.
/// </para>
/// </remarks>
[Element("static_select")]
public sealed record StaticSelectMenu() : SelectMenu("static_select"), IValueElement, IActionElement
{
    /// <summary>
    /// Constructs a <see cref="StaticSelectMenu"/> with the specified options.
    /// </summary>
    /// <param name="actionId">The action id for this menu.</param>
    /// <param name="options">The options to include in the menu.</param>
    public StaticSelectMenu(string actionId, params Option[] options) : this(options)
    {
        ActionId = actionId;
    }

    /// <summary>
    /// Constructs a <see cref="StaticSelectMenu"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to include in the menu.</param>
    public StaticSelectMenu(IEnumerable<Option> options) : this()
    {
        Options = options.ToList();
    }

    /// <summary>
    /// Constructs a <see cref="StaticSelectMenu"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to include in the menu.</param>
    public StaticSelectMenu(params Option[] options) : this()
    {
        Options = options;
    }

    /// <summary>
    /// Constructs a <see cref="StaticSelectMenu"/> with the specified option groups.
    /// </summary>
    /// <param name="actionId">The action id for this menu.</param>
    /// <param name="optionGroups">The options to include in the menu.</param>
    public StaticSelectMenu(string actionId, params OptionGroup[] optionGroups) : this(optionGroups)
    {
        ActionId = actionId;
    }

    /// <summary>
    /// Constructs a <see cref="StaticSelectMenu"/> with the specified option groups.
    /// </summary>
    /// <param name="optionGroups">The options to include in the menu.</param>
    public StaticSelectMenu(params OptionGroup[] optionGroups) : this()
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
    /// The selected <see cref="Option"/>.
    /// </summary>
    [JsonProperty("selected_option")]
    [JsonPropertyName("selected_option")]
    public Option? SelectedOption { get; init; }

    /// <summary>
    /// An array of <see cref="OptionGroup"/> instances. Maximum of 100 items.
    /// If <see cref="Options"/> is specified, this should not be.
    /// </summary>
    [JsonProperty("option_groups")]
    [JsonPropertyName("option_groups")]
    public IReadOnlyList<OptionGroup>? OptionGroups { get; init; }

    /// <summary>
    /// A single <see cref="Option"/> that exactly matches one of the
    /// options within <see cref="Options"/> or <see cref="OptionGroups"/>.
    /// This option will be selected when the menu initially loads.
    /// </summary>
    [JsonProperty("initial_option")]
    [JsonPropertyName("initial_option")]
    public Option? InitialOption { get; init; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    string? IValueElement.Value
    {
        get => SelectedOption?.Value;
        init => throw new InvalidOperationException();
    }
}

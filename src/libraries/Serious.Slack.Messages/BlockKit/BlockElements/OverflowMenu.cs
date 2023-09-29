using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// This is like a cross between a button and a select menu - when a user clicks on this
/// overflow button, they will be presented with a list of options to choose from. Unlike
/// the select menu, there is no typeahead field, and the button always appears with an
/// ellipsis ("…") rather than customisable text.
/// <para>
/// As such, it is usually used if you want a more compact layout than a select menu, or
/// to supply a list of less visually important actions after a row of buttons. You can
/// also specify simple URL links as overflow menu options, instead of actions.
/// </para>
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> and <see cref="Input"/>.
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#overflow"/>
/// </remarks>
[Element("overflow")]
public sealed record OverflowMenu() : InteractiveElement("overflow"), IValueElement, IActionElement
{
    /// <summary>
    /// Constructs an <see cref="OverflowMenu"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to display in the menu.</param>
    public OverflowMenu(IReadOnlyList<OverflowOption> options) : this()
    {
        Options = options;
    }

    /// <summary>
    /// Constructs an <see cref="OverflowMenu"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to display in the menu.</param>
    public OverflowMenu(params OverflowOption[] options) : this()
    {
        Options = options;
    }

    /// <summary>
    /// An array of <see cref="OverflowOption"/> instances to display in the menu.
    /// Maximum number of options is 5, minimum is 2.
    /// </summary>
    [JsonProperty("options")]
    [JsonPropertyName("options")]
    public IReadOnlyList<OverflowOption> Options { get; init; } = Array.Empty<OverflowOption>();

    /// <summary>
    /// An array of <see cref="OverflowOption"/> instances to display in the menu.
    /// Maximum number of options is 5, minimum is 2.
    /// </summary>
    [JsonProperty("selected_option")]
    [JsonPropertyName("selected_option")]
    public Option? SelectedOption { get; init; } // We never get the URL which is why this is an OptionObject.

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    string? IValueElement.Value
    {
        get => SelectedOption?.Value;
        init => throw new InvalidOperationException();
    }

    /// <summary>
    /// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
    /// dialog that appears after a menu item is selected.
    /// </summary>
    [JsonProperty("confirm")]
    [JsonPropertyName("confirm")]
    public ConfirmationDialog? Confirm { get; init; }
}

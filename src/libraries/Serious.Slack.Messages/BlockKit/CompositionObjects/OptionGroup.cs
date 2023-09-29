using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Provides a way to group options in a
/// <see href="https://api.slack.com/reference/block-kit/block-elements#select">select menu</see> or
/// <see href="https://api.slack.com/reference/block-kit/block-elements#multi_select">multi-select menu</see>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#option_group"/> for
/// more information.
/// </remarks>
/// <param name="Label">
/// A <c>plain_text</c> label shown above this group of options. Maximum length for this field is 75 characters.
/// </param>
public record OptionGroup(
    [property:JsonProperty("label")]
    [property:JsonPropertyName("label")]
    PlainText Label)
{
    /// <summary>
    /// Constructs an <see cref="OptionGroup"/>.
    /// </summary>
    public OptionGroup() : this("")
    {
    }

    /// <summary>
    /// Constructs an <see cref="OptionGroup"/> with the given label and set of options.
    /// </summary>
    /// <param name="label">A <c>plain_text</c> label shown above this group of options. Maximum length for this field is 75 characters.</param>
    /// <param name="options">The set of <see cref="Option"/>s in this group.</param>
    public OptionGroup(PlainText label, params Option[] options) : this(label)
    {
        Options = options;
    }

    /// <summary>
    /// An array of <see cref="Option"/> instances that belong to this specific group.
    /// Maximum of 100 items.
    /// </summary>
    [JsonProperty("options")]
    [JsonPropertyName("options")]
    public IReadOnlyList<Option> Options { get; init; } = Array.Empty<Option>();
}

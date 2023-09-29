using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A block that is used to hold interactive elements.
/// </summary>
/// <remarks>
/// <see href="https://api.slack.com/reference/block-kit/blocks#actions" /> for more information.
/// <para>
/// Available in surfaces: Modals Messages Home tabs
/// </para>
/// </remarks>
[Element("actions")]
public record Actions() : LayoutBlock("actions")
{
    /// <summary>
    /// Constructs an <see cref="Actions"/> block with the set of <see cref="IPayloadElement"/> instances.
    /// </summary>
    /// <param name="elements"></param>
    public Actions(params IActionElement[] elements) : this()
    {
        Elements = elements;
    }

    /// <summary>
    /// Constructs an <see cref="Actions"/> block with the set of <see cref="IPayloadElement"/> instances.
    /// </summary>
    /// <param name="blockId">The Block Id to use.</param>
    /// <param name="elements"></param>
    public Actions(string blockId, params IActionElement[] elements) : this(elements)
    {
        BlockId = blockId;
    }

    /// <summary>
    /// An array of interactive element objects - <see cref="ButtonElement"/> instances,
    /// <see cref="SelectMenu"/> instances, <see cref="OverflowMenu"/>,
    /// or <see cref="TimePicker"/> instances. There is a maximum of 5 elements
    /// in each action block.
    /// </summary>
    [JsonProperty("elements")]
    [JsonPropertyName("elements")]
    public IReadOnlyList<IActionElement> Elements { get; init; } = Array.Empty<IActionElement>();
}

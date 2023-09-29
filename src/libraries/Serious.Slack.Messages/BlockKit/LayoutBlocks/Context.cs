using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Displays message context, which can include both images and text.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#context" /> for more info.
/// <para>
/// Appears in surfaces: Modals, Messages, Home Tab.
/// </para>
/// </remarks>
[Element("context")]
public record Context() : LayoutBlock("context")
{
    /// <summary>
    /// Constructs a new <see cref="Context"/> block with a single <see cref="MrkdwnText"/> element containing the provided <paramref name="mrkdwnText"/>.
    /// </summary>
    /// <param name="mrkdwnText">The text to render in the Context block.</param>
    public Context(string mrkdwnText) : this(new MrkdwnText(mrkdwnText))
    {
    }

    /// <summary>
    /// Constructs a new <see cref="Context"/> with the set of elements.
    /// </summary>
    /// <param name="elements"></param>
    public Context(params IContextBlockElement[] elements) : this()
    {
        Elements = elements;
    }

    /// <summary>
    /// An array of <c>image</c> elements (<see cref="ImageElement"/>) and text objects such as <c>plain_text</c>
    /// (<see cref="PlainText"/>) and <c>mrkdwn</c> (<see cref="MrkdwnText"/>). instances.
    /// Maximum number of items is 10.
    /// </summary>
    [JsonProperty("elements")]
    [JsonPropertyName("elements")]
    public IReadOnlyList<IContextBlockElement> Elements { get; init; } = Array.Empty<IContextBlockElement>();
}

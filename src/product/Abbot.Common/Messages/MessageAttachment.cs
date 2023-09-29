using System;
using System.Collections.Generic;
// This type is serializable and deserializable. The property setters cannot be private.
// ReSharper disable MemberCanBePrivate.Global

namespace Serious.Abbot.Messages;

/// <summary>
/// The serialization format for a message attachment as part of a <see cref="ProactiveBotMessage" />.
/// </summary>
public class MessageAttachment
{
    /// <summary>
    /// The set of buttons to send, if any.
    /// </summary>
    public IReadOnlyList<ButtonMessage>? Buttons { get; init; } = Array.Empty<ButtonMessage>();

    /// <summary>
    /// The label to present for the set of buttons.
    /// </summary>
    public string? ButtonsLabel { get; init; }

    /// <summary>
    /// An image to render before the set of buttons. Either a URL or a base64 encoded string.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// The title for the hero card rendered when presenting buttons.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// If specified, makes the title a link to this URL.
    /// </summary>
    public string? TitleUrl { get; init; }

    /// <summary>
    /// Color for the attachment sidebar (Slack Only).
    /// </summary>
    public string? Color { get; init; }
}

using System;
using System.Net;

namespace Serious.Slack;

/// <summary>
/// Represents a Slack emoji by name. Try casting this to <see cref="UnicodeEmoji"/> or <see cref="CustomEmoji"/>.
/// </summary>
/// <param name="Name">The name of the emoji.</param>
public record Emoji(string Name);

/// <summary>
/// An emoji that can be represented by a unicode character. This contains the HTML entity for the emoji.
/// </summary>
/// <param name="Name">The name of the emoji.</param>
/// <param name="Emoji">The sequence of UTF-16 characters that represent the emoji.</param>
public record UnicodeEmoji(string Name, string Emoji) : Emoji(Name)
{
    /// <summary>
    /// The canonical name of the emoji.
    /// </summary>
    public string? CanonicalName { get; init; }

    /// <summary>
    /// Gets the HTML entity code(s) for the emoji.
    /// </summary>
    public string HtmlEncoded => WebUtility.HtmlEncode(Emoji);
}

/// <summary>
/// A custom emoji that can be represented by a custom image.
/// </summary>
/// <param name="Name">The name of the emoji.</param>
/// <param name="ImageUrl">The URL to the emoji.</param>
public record CustomEmoji(string Name, Uri ImageUrl) : Emoji(Name);

using System;

namespace Serious.Slack;

/// <summary>
/// Used to render a Slack link by calling `ToString()` since who can remember the order that the URL and Text go in?
/// </summary>
/// <param name="Url">The URL to link to.</param>
/// <param name="Text">The text of the link.</param>
public record Hyperlink(Uri Url, string Text)
{
    /// <summary>
    /// Returns the link using the Slack link format.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"<{Url}|{Text}>";
    }
}

using Markdig.Syntax;

namespace Serious.Razor.Components.Services;

/// <summary>
/// Service used to parse and render markdown content.
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Renders the specified markdown content as HTML.
    /// </summary>
    /// <param name="markdownContent">A string containing markdown formatted content.</param>
    string Render(string markdownContent);

    /// <summary>
    /// Parses the markdown formatted content into a <see cref="MarkdownDocument" />.
    /// </summary>
    /// <param name="markdownContent"></param>
    MarkdownDocument Parse(string markdownContent);
}

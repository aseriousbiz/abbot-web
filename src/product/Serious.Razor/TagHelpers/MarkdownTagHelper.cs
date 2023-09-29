using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.Razor.Components.Services;
using Westwind.AspNetCore.Markdown;

namespace Serious.Razor.TagHelpers;

/// <summary>
/// A tag helper that renders markdown as HTML. Adapted from https://github.com/RickStrahl/Westwind.AspNetCore.Markdown
/// but with stronger HTML sanitization.
/// </summary>
[HtmlTargetElement("markdown")]
public class MarkdownTagHelper : TagHelper
{
    readonly IMarkdownParserFactory _markdownParserFactory;

    public MarkdownTagHelper()
    {
        _markdownParserFactory = new MarkdigMarkdownParserFactory();
    }

    /// <summary>
    /// When true, allows sanitized HTML to be rendered.
    /// </summary>
    [HtmlAttributeName("allow-html")]
    public bool AllowHtml { get; set; }

    /// <summary>
    /// When set to true (default) strips leading white space based
    /// on the first line of non-empty content. The first line of
    /// content determines the format of the white spacing and removes
    /// it from all other lines.
    /// </summary>
    /// <remarks>
    /// When pulling markdown from a variable, to get full fidelity, you may want to do this:
    /// &lt;markdown normalize-whitespace="false"&gt;@yourMarkdown&lt;/markdown&gt; otherwise cases where the markdown
    /// you're rendering starts with an indented code block will not render correctly.
    /// </remarks>
    [HtmlAttributeName("normalize-whitespace")]
    public bool NormalizeWhitespace { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync(NullHtmlEncoder.Default)).GetContent(NullHtmlEncoder.Default);
        if (content is not { Length: > 0 })
        {
            return;
        }
        string markdown = NormalizeWhitespace ? NormalizeWhiteSpaceText(content) : content;
        var parser = _markdownParserFactory.GetParser();
        markdown = AllowHtml ? markdown : HttpUtility.HtmlEncode(markdown);
        string encoded = parser.Parse(markdown);
        var sanitized = MarkdownService.SanitizeHtml(encoded);
        output.TagName = string.Empty;
        output.Content.SetHtmlContent(sanitized);
        await base.ProcessAsync(context, output);
    }

    /// <summary>
    /// Strips leading white space based on shortest indented non-empty line.
    /// </summary>
    /// <remarks>
    /// The reason this method exists is indented markdown is considered to be preformatted code blocks. And the
    /// the markdown this tag helper receives might all be indented because of where the markdown tag helper is used
    /// within the page (such as indented within a div).
    /// </remarks>
    static string NormalizeWhiteSpaceText(string text)
    {
        if (text is { Length: 0 })
        {
            return text;
        }

        var lineReader = new StringReader(text);
        // In the case of something like this, the first line will be empty, so we need to advance to the first
        // non-empty line.
        // <markdown>
        //    @someMarkdown
        // </markdown>
        var firstLine = lineReader.ReadLine();
        while (firstLine is not { Length: > 0 })
        {
            firstLine = lineReader.ReadLine();
        }

        if (firstLine is not { Length: > 0 })
        {
            return text;
        }

        var indent = GetIndentation(firstLine); // We're going to use this to strip leading white space.

        return string.Join("\n", GetLines(lineReader, firstLine, indent));
    }

    static IEnumerable<string> GetLines(StringReader stringReader, string firstLine, int indent)
    {
        yield return firstLine[indent..];
        ;
        while (stringReader.ReadLine() is { } line)
        {
            var indentation = GetIndentation(line);
            yield return indentation < indent
                ? line
                : line[indent..];
        }
    }

    static int GetIndentation(string line)
    {
        int num = 0;
        while (num < line.Length && char.IsWhiteSpace(line[num]))
            ++num;
        return num;
    }
}

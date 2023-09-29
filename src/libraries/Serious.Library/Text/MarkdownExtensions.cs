using System;
using System.Text.RegularExpressions;

namespace Serious.Text;

public static class MarkdownExtensions
{
    // Matches exactly two backtick ('`') characters in a row.
    // Specifically, this matches a string if:
    //   * "(?<!`)" - It is _not_ preceded by a backtick '`'
    //   * "``" - It contains two backtick '`' characters in a row
    //   * "(?!`)" - It is _not_ followed by a backtick '`'
    static readonly Regex BacktickRegex = new("(?<!`)``(?!`)", RegexOptions.Compiled);

    /// <summary>
    /// Wraps the specified <paramref name="text"/> in a markdown inline code block
    /// </summary>
    /// <remarks>
    /// Unfortunately escaping inline code is terrible. There is no escape character for backticks. So if your
    /// content has a backtick, you can use a double backtick like this: `` ` `` which renders a single ` in an
    /// inline code block. But what if the content has a double backtick. Well, you're SOL. So we just replace those
    /// with a single backtick. Note that any other number of backticks is fine. Thus `` ```` `` is just fine.
    /// </remarks>
    /// <param name="text">The text to wrap.</param>
    /// <returns>An inline code block.</returns>
    public static string ToMarkdownInlineCode(this string? text)
    {
        if (text is null or { Length: 0 })
        {
            return string.Empty;
        }
        if (!text.Contains('`', StringComparison.Ordinal))
        {
            return $"`{text}`";
        }

        var escaped = BacktickRegex.Replace(text, "`");

        return $"`` {escaped} ``";
    }

    public static string ToMarkdownInlineCode(this Uri? url)
    {
        var urlText = url?.ToString();
        return urlText.ToMarkdownInlineCode();
    }

    public static string ToMarkdownInlineCode(this char c)
    {
        return c != '`'
            ? $"`{c}"
            : $"`` ` ``";
    }
}

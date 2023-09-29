using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Serious.Filters;

/// <summary>
/// Class used to parse a search string into a list of filters and the remaining text.
/// </summary>
public static partial class FilterParser
{
    /// <summary>
    /// Parse a search string into a list of filters and the remaining text.
    /// </summary>
    /// <param name="text">The search text.</param>
    /// <returns>A <see cref="FilterList"/>.</returns>
    public static FilterList Parse(string text) => new(ParseFilters(text));

    static IEnumerable<Filter> ParseFilters(string text)
    {
        var trimmed = text.Trim();
        var filterMatches = TokenRegex().Matches(trimmed);

        int previousIndex = 0;
        foreach (Match match in filterMatches)
        {
            if (match.Index > previousIndex)
            {
                var textBetweenFilters = trimmed[previousIndex..match.Index].Trim();
                if (!string.IsNullOrWhiteSpace(textBetweenFilters))
                {
                    yield return new Filter(textBetweenFilters);
                }
            }
            yield return Filter.Create(match.Groups["field"].Value, match.Groups["value"].Value, match.Value);
            previousIndex = match.Index + match.Length;
        }

        if (trimmed.Length > previousIndex)
        {
            var textAfterFilter = trimmed.Substring(previousIndex, trimmed.Length - previousIndex).Trim();
            if (!string.IsNullOrWhiteSpace(textAfterFilter))
            {
                yield return new Filter(textAfterFilter);
            }
        }
    }

    [GeneratedRegex(@"(?<field>-?\w+):(?:""(?<value>.+?)""|(?<value>[\w-]+))")]
    private static partial Regex TokenRegex();
}

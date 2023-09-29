using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Serious.Abbot.Signals;

/// <summary>
/// Extension methods for <see cref="PatternType"/>.
/// </summary>
public static class PatternTypeExtensions
{
    /// <summary>
    /// Returns a human readable description of the pattern type.
    /// </summary>
    /// <param name="patternType">The pattern type.</param>
    public static string Humanize(this PatternType patternType)
    {
        return patternType switch
        {
            PatternType.None => "none",
            PatternType.StartsWith => "start with",
            PatternType.EndsWith => "end with",
            PatternType.Contains => "contain",
            PatternType.RegularExpression => "match the regular expression",
            PatternType.ExactMatch => "is",
            _ => throw new UnreachableException("Unexpected pattern type")
        };
    }

    /// <summary>
    /// Returns whether the specified text matches the pattern and pattern type.
    /// </summary>
    /// <param name="patternType"></param>
    /// <param name="text"></param>
    /// <param name="pattern"></param>
    /// <param name="caseSensitive"></param>
    /// <returns></returns>
    public static bool IsMatch(this PatternType patternType, string text, string pattern, bool caseSensitive = true)
    {
        var stringComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return patternType switch
        {
            PatternType.StartsWith => text.StartsWith(pattern, stringComparison),
            PatternType.EndsWith => text.EndsWith(pattern, stringComparison),
            PatternType.Contains => text.Contains(pattern, stringComparison),
            PatternType.RegularExpression => IsRegexMatch(text, pattern, caseSensitive),
            PatternType.ExactMatch => text.Equals(pattern, stringComparison),
            _ => false
        };
    }

    static bool IsRegexMatch(string text, string pattern, bool caseSensitive)
    {
        var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        return Regex.IsMatch(text, pattern, regexOptions);
    }
}

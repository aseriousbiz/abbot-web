// LICENSE
//
//   This software is dual-licensed to the public domain and under the following
//   license: you are granted a perpetual, irrevocable license to copy, modify,
//   publish, and distribute this file as you see fit.
// CREDIT: https://gist.github.com/CDillinger/2aa02128f840bdca90340ce08ee71bc2

using System;
using System.Text.RegularExpressions;

namespace Serious.Text;

public static class FuzzyMatcher
{
    /// <summary>
    /// Does a fuzzy search for a pattern within a string.
    /// </summary>
    /// <param name="stringToSearch">The string to search for the pattern in.</param>
    /// <param name="pattern">The pattern to search for in the string.</param>
    /// <returns>true if each character in pattern is found sequentially within stringToSearch; otherwise, false.</returns>
    public static bool FuzzyMatch(this string stringToSearch, string pattern)
    {
        var match = stringToSearch.FuzzyMatch(pattern, out var score);
        return match && score > 0 || score > pattern.Length;
    }

    /// <summary>
    /// Does a fuzzy search for a pattern within a string, and gives the search a score on how well it matched.
    /// </summary>
    /// <param name="stringToSearch">The string to search for the pattern in.</param>
    /// <param name="pattern">The pattern to search for in the string.</param>
    /// <param name="outScore">The score which this search received, if a match was found.</param>
    /// <returns>true if each character in pattern is found sequentially within stringToSearch; otherwise, false.</returns>
    public static bool FuzzyMatch(this string stringToSearch, string pattern, out int outScore)
    {
        stringToSearch = NormalizePattern(stringToSearch);
        pattern = NormalizePattern(pattern);

        // Score consts
        const int adjacencyBonus = 5;               // bonus for adjacent matches
        const int separatorBonus = 10;              // bonus if match occurs after a separator
        const int camelBonus = 10;                  // bonus if match is uppercase and prev is lower

        const int leadingLetterPenalty = -3;        // penalty applied for every letter in stringToSearch before the first match
        const int maxLeadingLetterPenalty = -9;     // maximum penalty for leading letters
        const int unmatchedLetterPenalty = -1;      // penalty for every letter that doesn't matter

        // Loop variables
        var score = 0;
        var patternIdx = 0;
        var patternLength = pattern.Length;
        var strIdx = 0;
        var strLength = stringToSearch.Length;
        var prevMatched = false;
        var prevLower = false;
        var prevSeparator = true;                   // true if first letter match gets separator bonus

        // Use "best" matched letter if multiple string letters match the pattern
        char? bestLetter = null;
        char? bestLower = null;
        var bestLetterScore = 0;

        // Loop over strings
        while (strIdx != strLength)
        {
            var patternChar = patternIdx != patternLength ? pattern[patternIdx] as char? : null;
            var strChar = stringToSearch[strIdx];

            var patternLower = patternChar is not null ? char.ToLowerInvariant((char)patternChar) as char? : null;
            var strLower = char.ToLowerInvariant(strChar);
            var strUpper = char.ToUpperInvariant(strChar);

            var nextMatch = patternChar is not null && patternLower == strLower;
            var rematch = bestLetter is not null && bestLower == strLower;

            var advanced = nextMatch && bestLetter is not null;
            var patternRepeat = bestLetter is not null && patternChar is not null && bestLower == patternLower;
            if (advanced || patternRepeat)
            {
                score += bestLetterScore;
                bestLetter = null;
                bestLower = null;
                bestLetterScore = 0;
            }

            if (nextMatch || rematch)
            {
                var newScore = 0;

                // Apply penalty for each letter before the first pattern match
                // Note: Math.Max because penalties are negative values. So max is smallest penalty.
                if (patternIdx == 0)
                {
                    var penalty = Math.Max(strIdx * leadingLetterPenalty, maxLeadingLetterPenalty);
                    score += penalty;
                }

                // Apply bonus for consecutive bonuses
                if (prevMatched)
                    newScore += adjacencyBonus;

                // Apply bonus for matches after a separator
                if (prevSeparator)
                    newScore += separatorBonus;

                // Apply bonus across camel case boundaries. Includes "clever" isLetter check.
                if (prevLower && strChar == strUpper && strLower != strUpper)
                    newScore += camelBonus;

                // Update pattern index IF the next pattern letter was matched
                if (nextMatch)
                    ++patternIdx;

                // Update best letter in stringToSearch which may be for a "next" letter or a "rematch"
                if (newScore >= bestLetterScore)
                {
                    // Apply penalty for now skipped letter
                    if (bestLetter is not null)
                        score += unmatchedLetterPenalty;

                    bestLetter = strChar;
                    bestLower = char.ToLowerInvariant((char)bestLetter);
                    bestLetterScore = newScore;
                }

                prevMatched = true;
            }
            else
            {
                score += unmatchedLetterPenalty;
                prevMatched = false;
            }

            // Includes "clever" isLetter check.
            prevLower = strChar == strLower && strLower != strUpper;
            prevSeparator = strChar is '_' or ' ';

            ++strIdx;
        }

        // Apply score for last match
        if (bestLetter is not null)
        {
            score += bestLetterScore;
        }

        outScore = score;
        return patternIdx == patternLength;
    }

    static readonly Regex CharactersToKeep = new Regex(@"[^\w\s-_]", RegexOptions.Compiled);
    static readonly Regex NormalizeRegex = new Regex(@"\b(\w|an|is|to|of|the|Mr|Mrs|Ms)\s*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>
    /// Strip punctuation and filler words like "And" "Or" "The", "Of" etc.
    /// </summary>
    /// <param name="text">The text to normalize</param>
    public static string NormalizePattern(string text)
    {
        return NormalizeRegex.Replace(CharactersToKeep.Replace(text, string.Empty), string.Empty);
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Serious.Abbot.Skills;

// Common patterns used when parsing text to match skills.
// A great place to test your regex patterns is http://regexstorm.net/tester
public static class SkillPatterns
{
    internal const string NamePattern = "[\\w-._]+";
    internal const string HeyPattern = @"(?:(?:hey|yo|hi(ya)?|hello|howdy)\s+)?";

    internal static string GetMentionPattern(string groupName)
    {
        return $@"<@(?<{groupName}>.+?)>"; // Abbot Normal Form for mentions (aka Slack's mention format).
    }

    const string KeyPattern = @"(?:""(?<key>.*?)""|'(?<key>.*?)'|(?<key>.*?))";
    const string SearchPattern = @"(?<key>\|)\s*(?<value>.*)";

    static readonly Regex RememberRegex = new(
        $@"^{SearchPattern}|(?:{KeyPattern}(?:\s+(?:is|to|=)\s+(?<value>.*?))?)$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static (string key, string value) MatchRememberPattern(string args)
    {
        var match = RememberRegex.Match(args);
        return (match.Groups["key"].Value, match.Groups["value"].Value);
    }

    const string EchoArgsPattern =
        @"^(?<thread>!thread\s*)?(?:room:(?<room>\w+)\s*)?(?:user:(?<user>\w+)\s*)?(?:format:(?<format>\w+)\s*)?(?<text>.*?)$";
    static readonly Regex EchoRegex = new(EchoArgsPattern, RegexOptions.Compiled | RegexOptions.Singleline);

    public static (bool thread, string echoText, string format, string user, string roomId) MatchEchoPattern(string args)
    {
        var match = EchoRegex.Match(args);
        return match.Success
            ? (
                match.Groups["thread"].Success,
                match.Groups["text"].Value,
                match.Groups["format"].Value,
                match.Groups["user"].Value,
                match.Groups["room"].Value
            )
            : (false, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    static readonly Regex MentionsRegex = new(
        $@"(?<=^|\s){GetMentionPattern("user")}(?=\s|$)",
        RegexOptions.Compiled);

    public static IEnumerable<string> ParseMentions(string args)
    {
        var matches = MentionsRegex.Matches(args);
        foreach (Match match in matches)
        {
            yield return match.Groups["user"].Value;
        }
    }
}

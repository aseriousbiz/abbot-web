using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serious.Abbot.Messaging;

namespace Serious.Slack.Parsing;

/// <summary>
/// Provides helper methods for parsing Slack messages in the "mrkdwn" format.
/// <seealso href="https://api.slack.com/reference/surfaces/formatting" />
/// </summary>
public static class MrkdwnParser
{
    static readonly Regex TokenFinder = new(@"(?:<(?<content>.+?)>|:(?<content>\S+?):)|\*(?<content>.+?)\*|_(?<content>.+?)_|~(?<content>.+?)~|`(?<content>.+?)`");

    public static MrkdwnMessage Parse(string input)
    {
        var spans = ParseSpans(input).ToList();
        return new MrkdwnMessage(spans);
    }

    static IEnumerable<MrkdwnSpan> ParseSpans(string input)
    {
        // Based on https://api.slack.com/reference/surfaces/formatting#retrieving-messages
        var matches = TokenFinder.Matches(input);
        int start = 0;
        foreach (var match in matches.Cast<Match>())
        {
            if (match.Index > start)
            {
                yield return new MrkdwnPlainText(input[start..match.Index]);
            }
            var span = ParseSpan(match.Value, match.Groups[1].Value);
            yield return span;
            start = match.Index + match.Length;
        }

        if (start < input.Length)
        {
            yield return new MrkdwnPlainText(input[start..]);
        }
    }

    static MrkdwnSpan ParseSpan(string originalText, string spanContent)
    {
        var labelIdx = spanContent.IndexOf('|', StringComparison.Ordinal);
        var (content, label) = labelIdx >= 0
            ? (spanContent[..labelIdx], spanContent[(labelIdx + 1)..])
            : (spanContent, null);

        if (content.StartsWith("#C", StringComparison.Ordinal))
        {
            return new MrkdwnMention(originalText, SlackMentionType.Channel, content[1..], label);
        }

        if (content.StartsWith("@U", StringComparison.Ordinal) || content.StartsWith("@W", StringComparison.Ordinal))
        {
            return new MrkdwnMention(originalText, SlackMentionType.User, content[1..], label);
        }

        if (content.StartsWith("!subteam", StringComparison.Ordinal))
        {
            var caretIndex = content.IndexOf('^', StringComparison.Ordinal);
            if (caretIndex < 0)
            {
                return new MrkdwnPlainText(originalText);
            }
            return new MrkdwnMention(originalText, SlackMentionType.UserGroup, content[(caretIndex + 1)..], label);
        }

        if (originalText.StartsWith(':'))
        {
            return new MrkdwnEmoji(originalText, content);
        }


        if (content.StartsWith('!'))
        {
            var type = content switch
            {
                "!here" => SlackMentionType.AtHere,
                "!channel" => SlackMentionType.AtChannel,
                "!everyone" => SlackMentionType.AtEveryone,
                _ => SlackMentionType.Unknown
            };

            // We don't support dates right now.
            if (type == SlackMentionType.Unknown)
            {
                return new MrkdwnPlainText(originalText);
            }

            return new MrkdwnMention(originalText, type, null, label);
        }

        if (originalText[0] is var formatChar and ('*' or '_' or '~' or '`'))
        {
            var format = formatChar switch
            {
                '*' => TextFormat.Bold,
                '_' => TextFormat.Italic,
                '~' => TextFormat.Strike,
                '`' => TextFormat.Code,
                _ => throw new ArgumentOutOfRangeException(nameof(originalText), formatChar, null)
            };

            var inner = ParseSpans(content);
            return new MrkdwnFormattedSpans(originalText, inner.ToList(), format);
        }

        var linkSpans = label is null
            ? new List<MrkdwnSpan> { new MrkdwnPlainText(content) }
            : ParseSpans(label).ToList();
        return new MrkdwnLink(originalText, content, linkSpans);
    }
}

public class MrkdwnMessage
{
    public MrkdwnMessage(IReadOnlyList<MrkdwnSpan> spans)
    {
        Spans = spans;
    }

    public IReadOnlyList<MrkdwnSpan> Spans { get; }
}

public enum SlackMentionType
{
    Unknown,
    User,
    Channel,
    UserGroup,
    AtHere,
    AtChannel,
    AtEveryone
}

public abstract record MrkdwnSpan(string OriginalText);
public record MrkdwnPlainText(string Text) : MrkdwnSpan(Text);

public record MrkdwnFormattedSpans(string OriginalText, IReadOnlyList<MrkdwnSpan> FormattedSpans, TextFormat Format)
    : MrkdwnSpan(OriginalText)
{
    public virtual bool Equals(MrkdwnFormattedSpans? other)
    {
        return other is not null && other.OriginalText == OriginalText
                                 && other.Format == Format
                                 && other.FormattedSpans.SequenceEqual(FormattedSpans);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), OriginalText, (int)Format);
    }
};

public record MrkdwnEmoji(string Text, string EmojiName) : MrkdwnSpan(Text);
public record MrkdwnMention(string OriginalText, SlackMentionType Type, string? Id, string? Label) : MrkdwnSpan(OriginalText);

public record MrkdwnLink(
    string OriginalText,
    string Url,
    IReadOnlyList<MrkdwnSpan> FormattedSpans)
    : MrkdwnFormattedSpans(OriginalText, FormattedSpans, TextFormat.Link);

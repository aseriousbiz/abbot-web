using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Slack;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Represents a message that has been formatted for rendering (e.g. Mentions have been resolved, etc.)
/// </summary>
public class RenderedMessage
{
    public RenderedMessage(IReadOnlyList<RenderedMessageSpan> spans)
    {
        Spans = spans;
    }

    public IReadOnlyList<RenderedMessageSpan> Spans { get; }

    public string ToText() => string.Join("", Spans.Select(span => span.ToText()));

    public string ToHtml() => string.Join("", Spans.Select(span => span.ToHtml()));
}

public abstract record RenderedMessageSpan(string OriginalText)
{
    public abstract string ToText();

    public abstract string ToHtml();
}

public record PlainTextSpan(string Text) : RenderedMessageSpan(Text)
{
    public override string ToText() => Text;
    public override string ToHtml() => Text;
}

public record FormattedTextSpan(
    string OriginalText,
    IReadOnlyList<RenderedMessageSpan> InnerSpans,
    TextFormat Format = TextFormat.PlainText) : RenderedMessageSpan(OriginalText)
{
    public override string ToText() => OriginalText;
    public override string ToHtml() => OriginalText;
}

public enum TextFormat
{
    PlainText,
    Bold,
    Italic,
    Strike,
    Code,
    Link
}

public record LinkSpan(string OriginalText, string LinkUrl, IReadOnlyList<RenderedMessageSpan> InnerSpans)
    : FormattedTextSpan(OriginalText, InnerSpans, TextFormat.Link)
{
    public override string ToText() => OriginalText;

    public override string ToHtml() => $"<a href=\"{LinkUrl}\">{OriginalText}</a>";

    public virtual bool Equals(LinkSpan? other)
    {
        return other is not null
               && other.OriginalText == OriginalText
               && other.LinkUrl == LinkUrl
               && other.InnerSpans.SequenceEqual(InnerSpans);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), LinkUrl, OriginalText);
    }
}

public record EmojiSpan(string Text, Emoji? Emoji) : RenderedMessageSpan(Text)
{
    public override string ToText() => Text;
    public override string ToHtml() => Text;
}

public record UserMentionSpan(string OriginalText, string Id, Member? Member) : RenderedMessageSpan(OriginalText)
{
    public override string ToText() => Member?.DisplayName ?? $"(unknown user {Id})";

    public override string ToHtml() => Member != null
        ? $@"<a href=""{SlackFormatter.UserUrl(Member.Organization.Domain, Member.User.PlatformUserId)}"">{Member.DisplayName}</a>"
        : $"(unknown user {Id})";
}

public record RoomMentionSpan(string OriginalText, string Id, Room? Room) : RenderedMessageSpan(OriginalText)
{
    public override string ToText() => Room is not null ? $"#{Room.Name}" : $"(unknown channel {Id})";

    public override string ToHtml() => Room != null
        ? $@"<a href=""{SlackFormatter.RoomUrl(Room.Organization.Domain, Room.PlatformRoomId)}"">#{Room.Name}</a>"
        : $"(unknown channel {Id})";
}

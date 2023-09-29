using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Infrastructure.TagHelpers;

public class MessageRendererTagHelper : TagHelper
{
    static readonly ILogger<MessageRendererTagHelper> Log =
        ApplicationLoggerFactory.CreateLogger<MessageRendererTagHelper>();
    public RenderedMessage? Message { get; set; }

    /// <summary>
    /// If <c>true</c>, links in the message will be rendered as links.
    /// </summary>
    public bool RenderLinks { get; set; }

    /// <summary>
    /// If <c>true</c>, the message, inserts br tags for newlines.
    /// </summary>
    public bool RenderNewlines { get; set; }

    public bool Editable { get; set; }

    /// <summary>
    /// This is a "soft" truncate. The rendered message is not guaranteed to be this length or less, but it'll be
    /// close. This truncates around word boundaries and doesn't split up words, mentions, etc.
    /// </summary>
    public int? TruncateLength { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Message is not { Spans.Count: > 0 })
        {
            output.SuppressOutput();
            return;
        }

        output.Content.Clear();
        output.TagName = "div";
        if (Editable)
        {
            output.Attributes.Add("contentEditable", "true");
            output.Attributes.Add("data-editable-target", "content");
        }

        output.TagMode = TagMode.StartTagAndEndTag;
        int charactersRemaining = TruncateLength ?? int.MaxValue;

        try
        {
            var tokens = Message.Spans.Select(span => Token.FromSpan(span, RenderLinks, RenderNewlines));

            foreach (var token in tokens)
            {
                token.Append(output.Content, charactersRemaining);

                charactersRemaining -= token.ContentLength;
                if (charactersRemaining <= 0)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // We don't want a tag helper to break the whole page rendering.
            output.Content.Clear();
            output.Content.AppendHtml("""<code class="text-red-500">An error occurred while rendering this message. Abbot Support has been notified and is working on the problem!</code>""");
            Log.ErrorRenderingMessage(ex);
        }
    }

    abstract record Token(int ContentLength, bool RenderNewlines = false)
    {
        public abstract void Append(TagHelperContent tagHelperContent, int charactersRemaining);

        protected void AppendContent(TagHelperContent tagHelperContent, string content)
        {
            if (RenderNewlines)
            {
                var lines = content.Split("\n");
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    tagHelperContent.Append(lines[i]);
                    tagHelperContent.AppendHtml("<br />");
                }

                tagHelperContent.Append(lines[^1]);
            }
            else
            {
                tagHelperContent.Append(content);
            }
        }

        static string RoomName(Room room)
        {
            return room.Name ?? "unknown";
        }

        static string GetTagForFormat(TextFormat format) =>
            format switch
            {
                TextFormat.Bold => "strong",
                TextFormat.Italic => "em",
                TextFormat.Strike => "s",
                TextFormat.Code => "code",
                TextFormat.PlainText => "span",
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };

        public static Token FromSpan(RenderedMessageSpan span, bool renderLinks, bool renderNewLines)
        {
            return span switch
            {
                LinkSpan { InnerSpans: { } innerSpans } linkSpan => renderLinks
                    ? new LinkToken(innerSpans, linkSpan.LinkUrl, renderNewLines)
                    : new TextToken(linkSpan.OriginalText.Length, linkSpan.OriginalText, renderNewLines),
                FormattedTextSpan { InnerSpans: { } innerSpans } formattedSpan
                    => new HtmlContainerTag(innerSpans, $"<{GetTagForFormat(formattedSpan.Format)}>", $"</{GetTagForFormat(formattedSpan.Format)}>"),
                PlainTextSpan { Text: { Length: > 0 } t }
                    => new TextToken(t.Length, t, renderNewLines),

                EmojiSpan emojiSpan => GetTagFromEmojiSpan(emojiSpan),
                UserMentionSpan { Member: { } m } => new HtmlTag(m.User.DisplayName.Length,
                    "<span class=\"msg msg-mention\">", $"@{m.DisplayName}", "</span>"),
                RoomMentionSpan { Room: { } r } => new HtmlTag(RoomName(r).Length,
                    "<span class=\"msg msg-mention\">", $"#{RoomName(r)}", "</span>"),
                UserMentionSpan { Id: { Length: > 0 } id } => new HtmlTag(id.Length,
                    "<span data-tooltip=\"Sorry, Abbot doesn't know about this user for some reason.\" class=\"msg msg-mention msg-mention-unknown\">", $"@{id}", "</span>"),
                RoomMentionSpan { Id: { Length: > 0 } id } => new HtmlTag(id.Length,
                    "<span data-tooltip=\"Sorry, Abbot doesn't know about this room for some reason.\" class=\"msg msg-mention msg-mention-unknown\">", $"#{id}", "</span>"),
                _ => new TextToken(0, "", RenderNewLines: false)
            };
        }

        static Token GetTagFromEmojiSpan(EmojiSpan emojiSpan)
        {
            return emojiSpan.Emoji switch
            {
                CustomEmoji customEmoji => new HtmlTag(1, @$"<img src=""{customEmoji.ImageUrl}"" class=""emoji"" alt=""{customEmoji.Name}"" />"),
                UnicodeEmoji unicodeEmoji => new HtmlEntity(unicodeEmoji.Emoji),
                _ => new TextToken(emojiSpan.Text.Length, emojiSpan.Text, NoTruncate: true, RenderNewLines: false)
            };
        }

        internal static string TruncateText(string text, int charactersRemaining)
        {
            const int hardOverflowLimit = 32;
            var truncated = text.TruncateAtWordBoundary(charactersRemaining, appendEllipses: true);
            if (truncated.Length > charactersRemaining && truncated.Length - charactersRemaining > hardOverflowLimit)
            {
                return text.TruncateToLength(hardOverflowLimit, appendEllipses: true);
            }

            return truncated;
        }
    }

    record HtmlContainerTag(int ContentLength, string StartTag, string? EndTag = null) : Token(ContentLength)
    {
        public IReadOnlyList<Token> InnerTokens { get; } = Array.Empty<Token>();

        public HtmlContainerTag(IEnumerable<RenderedMessageSpan> innerSpans, string startTag, string? endTag = null)
            : this(GetTokens(innerSpans, renderLinks: false, renderNewLines: false), startTag, endTag)
        {
        }

        public HtmlContainerTag(IReadOnlyList<Token> innerTokens, string startTag, string? endTag = null)
            : this(innerTokens.Select(t => t.ContentLength).Sum(), startTag, endTag)
        {
            InnerTokens = innerTokens;
            ContentLength = InnerTokens.Select(t => t.ContentLength).Sum();
        }


        static IReadOnlyList<Token> GetTokens(IEnumerable<RenderedMessageSpan> spans, bool renderLinks, bool renderNewLines)
        {
            return spans.Select(span => FromSpan(span, renderLinks, renderNewLines)).ToList();
        }

        public override void Append(TagHelperContent tagHelperContent, int charactersRemaining)
        {
            tagHelperContent.AppendHtml(StartTag);
            foreach (var token in InnerTokens)
            {
                token.Append(tagHelperContent, charactersRemaining);
                charactersRemaining -= token.ContentLength;
                if (charactersRemaining <= 0)
                {
                    break;
                }
            }

            if (EndTag is not null)
            {
                tagHelperContent.AppendHtml(EndTag);
            }
        }
    }

    record HtmlTag(int ContentLength, string StartTag, string? Content = null, string? EndTag = null)
        : Token(ContentLength)
    {
        public override void Append(TagHelperContent tagHelperContent, int charactersRemaining)
        {
            tagHelperContent.AppendHtml(StartTag);
            if (Content is not null)
            {
                tagHelperContent.Append(Content);
            }

            if (EndTag is not null)
            {
                tagHelperContent.AppendHtml(EndTag);
            }
        }
    }

    record HtmlEntity(string HtmlEntityCode) : Token(1)
    {
        public override void Append(TagHelperContent tagHelperContent, int charactersRemaining)
        {
            tagHelperContent.AppendHtml(HtmlEntityCode);
        }
    }

    record LinkToken(int ContentLength, string LinkUrl, bool RenderNewLines)
        : Token(ContentLength, RenderNewLines)
    {
        public IReadOnlyList<Token> InnerTokens { get; } = Array.Empty<Token>();

        public LinkToken(IEnumerable<RenderedMessageSpan> innerSpans, string linkUrl, bool renderNewLines)
            : this(GetTokens(innerSpans, renderLinks: false, renderNewLines: false), linkUrl, renderNewLines)
        {
        }

        public LinkToken(IReadOnlyList<Token> innerTokens, string linkUrl, bool renderNewLines)
            : this(innerTokens.Select(t => t.ContentLength).Sum(), linkUrl, renderNewLines)
        {
            InnerTokens = innerTokens;
        }

        public override void Append(TagHelperContent tagHelperContent, int charactersRemaining)
        {
            tagHelperContent.AppendHtml($"<a href=\"{LinkUrl}\">");
            foreach (var token in InnerTokens)
            {
                token.Append(tagHelperContent, charactersRemaining);
                charactersRemaining -= token.ContentLength;
                if (charactersRemaining <= 0)
                {
                    break;
                }
            }
            tagHelperContent.AppendHtml("</a>");
        }

        static IReadOnlyList<Token> GetTokens(IEnumerable<RenderedMessageSpan> spans, bool renderLinks, bool renderNewLines)
        {
            return spans.Select(span => FromSpan(span, renderLinks, renderNewLines)).ToList();
        }
    }

    record TextToken(int ContentLength, string Text, bool RenderNewLines, bool NoTruncate = false)
        : Token(ContentLength, RenderNewLines)
    {
        public override void Append(TagHelperContent tagHelperContent, int charactersRemaining)
        {
            var text = NoTruncate
                ? Text
                : TruncateText(Text, charactersRemaining);

            AppendContent(tagHelperContent, text);
        }
    }
}

public static partial class MessageRendererTagHelperLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An error occurred while rendering a Slack message.")]
    public static partial void ErrorRenderingMessage(this ILogger logger, Exception ex);
}

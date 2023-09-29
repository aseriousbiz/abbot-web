using Serious.Abbot.Entities;
using Serious.Slack.Parsing;
using Serious.Tasks;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Creates a <see cref="RenderedMessage"/> from a mrkdwn string.
/// </summary>
public interface IMessageRenderer
{
    /// <summary>
    /// Parses mrkdwn into a <see cref="RenderedMessage"/> structure which includes resolved mentions, links, etc.
    /// </summary>
    /// <param name="text">The mrkdwn text to render.</param>
    /// <param name="organization">The organization.</param>
    /// <returns>A <see cref="RenderedMessage"/>.</returns>
    Task<RenderedMessage> RenderMessageAsync(string? text, Organization organization);
}

public class MessageRenderer : IMessageRenderer
{
    readonly ISlackResolver _slackResolver;
    readonly IEmojiLookup _emojiLookup;

    public MessageRenderer(ISlackResolver slackResolver, IEmojiLookup emojiLookup)
    {
        _slackResolver = slackResolver;
        _emojiLookup = emojiLookup;
    }

    public async Task<RenderedMessage> RenderMessageAsync(string? text, Organization organization) =>
        text is null
            ? new RenderedMessage(Array.Empty<RenderedMessageSpan>())
            : await RenderSlackMessageAsync(text, organization);

    async Task<RenderedMessage> RenderSlackMessageAsync(string text, Organization organization)
    {
        var mrkdwn = MrkdwnParser.Parse(text);

        var renderedSpans = await mrkdwn
            .Spans
            .SelectFunc(s => FromMrkdwnSpanAsync(s, organization))
            .WhenAllOneAtATimeNotNullAsync();

        return new RenderedMessage(renderedSpans);
    }

    async Task<RenderedMessageSpan> FromMrkdwnSpanAsync(MrkdwnSpan mrkdwnSpan, Organization organization)
    {
        return mrkdwnSpan switch
        {
            MrkdwnEmoji emoji => await ResolveEmojiAsync(emoji, organization),
            MrkdwnPlainText plainText => new PlainTextSpan(plainText.Text),
            MrkdwnLink link => await RenderFormattedLinkSpans(organization, link),
            MrkdwnFormattedSpans formattedTextSpans => await RenderFormattedSpans(organization, formattedTextSpans),
            MrkdwnMention mention => await ResolveSlackMentionAsync(mention, organization),
            _ => new PlainTextSpan(mrkdwnSpan.OriginalText),
        };
    }

    async Task<RenderedMessageSpan> RenderFormattedSpans(Organization organization, MrkdwnFormattedSpans formatted)
    {
        var tasks = formatted.FormattedSpans.SelectFunc(s => FromMrkdwnSpanAsync(s, organization));
        var inner = await tasks.WhenAllOneAtATimeNotNullAsync();
        return new FormattedTextSpan(
            formatted.OriginalText,
            inner,
            formatted.Format);
    }

    async Task<RenderedMessageSpan> RenderFormattedLinkSpans(Organization organization, MrkdwnLink link)
    {
        var tasks = link.FormattedSpans.SelectFunc(s => FromMrkdwnSpanAsync(s, organization));
        var inner = await tasks.WhenAllOneAtATimeNotNullAsync();
        return new LinkSpan(
            link.OriginalText,
            link.Url,
            inner);
    }

    async Task<EmojiSpan> ResolveEmojiAsync(MrkdwnEmoji parsedEmoji, Organization organization)
    {
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return new EmojiSpan(parsedEmoji.Text, null);
        }

        var emoji = await _emojiLookup.GetEmojiAsync(parsedEmoji.EmojiName, apiToken);
        return new EmojiSpan(parsedEmoji.Text, emoji);
    }

    async Task<RenderedMessageSpan> ResolveSlackMentionAsync(MrkdwnMention mention, Organization organization)
    {
        switch (mention)
        {
            case { Type: SlackMentionType.User, Id.Length: > 0 }:
                var member = await _slackResolver.ResolveMemberAsync(mention.Id, organization);
                return new UserMentionSpan(mention.OriginalText, mention.Id, member);
            case { Type: SlackMentionType.Channel, Id.Length: > 0 }:
                var room = await _slackResolver.ResolveRoomAsync(mention.Id, organization, false);
                return new RoomMentionSpan(mention.OriginalText, mention.Id, room);
            case { Type: SlackMentionType.AtChannel }:
                return new PlainTextSpan("@channel");
            case { Type: SlackMentionType.AtHere }:
                return new PlainTextSpan("@here");
            case { Type: SlackMentionType.AtEveryone }:
                return new PlainTextSpan("@everyone");
            default:
                return new PlainTextSpan(mention.OriginalText);
        }
    }
}

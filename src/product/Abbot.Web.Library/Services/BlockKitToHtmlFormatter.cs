using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messaging;
using Serious.Logging;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Services;

public class BlockKitToHtmlFormatter
{
    static readonly ILogger<BlockKitToHtmlFormatter> Log = ApplicationLoggerFactory.CreateLogger<BlockKitToHtmlFormatter>();

    readonly ISlackResolver _slackResolver;

    public BlockKitToHtmlFormatter(ISlackResolver slackResolver)
    {
        _slackResolver = slackResolver;
    }

    public async Task<string> FormatBlocksAsHtmlAsync(
        IEnumerable<ILayoutBlock> blocks,
        Organization organization,
        HtmlFormatting formatting = HtmlFormatting.None)
    {
        var mentions = new Dictionary<string, string?>();

        var writer = new HtmlBuilder(formatting);
        foreach (var block in blocks.OfType<RichTextBlock>())
        {
            foreach (var section in block.Elements.OfType<RichTextSection>())
            {
                await WriteRichTextSectionHtml(writer, organization, section, mentions);
            }
        }

        return writer.ToString();
    }

    async Task WriteRichTextSectionHtml(
        HtmlBuilder builder,
        Organization organization,
        RichTextSection section,
        IDictionary<string, string?> mentions,
        bool insideInlineContent = false)
    {
        async Task AppendSectionElements(bool allowStyle)
        {
            foreach (var textElement in section.Elements)
            {
                await FormatElementAsync(builder, textElement, allowStyle, organization, mentions);
            }
        }

        async Task AppendBlockElement(string tagName, bool allowStyle)
        {
            using (builder.AppendTag(tagName))
            {
                await AppendSectionElements(allowStyle);
            }
        }

        switch (section)
        {
            case RichTextList list:
                await AppendList(builder, organization, section, mentions, list);
                break;
            case RichTextQuote:
                await AppendBlockElement("blockquote", allowStyle: true);
                break;
            case RichTextPreformatted:
                await AppendBlockElement("pre", allowStyle: false);
                break;
            default:
                await AppendSectionElements(allowStyle: true);
                if (builder.Formatting is HtmlFormatting.Indented && !insideInlineContent)
                {
                    builder.AppendLine();
                }
                break;
        }
    }


    async Task AppendList(
        HtmlBuilder builder,
        Organization organization,
        RichTextSection section,
        IDictionary<string, string?> mentions,
        RichTextList list)
    {
        var style = $"padding-left: {list.Indent * 16}px;"
                    + (list.Border > 0 ? " border-left: solid 4px #999;" : null);

        using (builder.AppendTag("div", style))
        {
            var listTagName = list.Style switch
            {
                RichTextListStyle.Bullet => "ul",
                RichTextListStyle.Ordered => "ol",
                _ => throw new UnreachableException($"Unexpected list style: {list.Style}.")
            };
            using (builder.Indent().AppendTag(listTagName, "margin: 0 0 0 24px;"))
            {
                foreach (var listItem in section.Elements.OfType<RichTextSection>())
                {
                    var listStyle = (list.Indent % 3) switch
                    {
                        0 => "disc",
                        1 => "circle",
                        2 => "square",
                        _ => throw new UnreachableException(),
                    };
                    using (builder
                           .Indent()
                           .Indent()
                           .AppendTag("li", $"margin: 0; padding: 4px 0; list-style-type: {listStyle};"))
                    {
                        await WriteRichTextSectionHtml(builder, organization, listItem, mentions, insideInlineContent: true);
                    }
                }
            }
        }
    }

    async Task FormatElementAsync(
        HtmlBuilder builder,
        IElement element,
        bool allowStyle,
        Organization organization,
        IDictionary<string, string?> mentions)
    {
        var cssStyle = new List<string>();
        string? text;
        string? linkUrl = null;
        if (element is LinkElement link)
        {
            // Naked links have Text = null
            text = link.Text ?? link.Url;
            linkUrl = link.Url;
        }
        else if (element is TextElement textElement)
        {
            text = textElement.Text;
        }
        else if (element is ChannelMention channelMention)
        {
            var channelId = channelMention.ChannelId;
            if (!mentions.TryGetValue(channelId, out var channelName))
            {
                var channel = await _slackResolver.ResolveRoomAsync(channelId, organization, forceRefresh: true);
                channelName = channel?.Name;
                mentions.Add(channelId, channelName);
            }
            text = channelName is not null or "" ? $"#{channelName}" : "(unknown channel)";
            linkUrl = SlackFormatter.RoomUrl(organization.Domain, channelId).ToString();
        }
        else if (element is UserMention userMention)
        {
            var userId = userMention.UserId;
            if (!mentions.TryGetValue(userId, out var userName))
            {
                var member = await _slackResolver.ResolveMemberAsync(userId, organization, forceRefresh: true);
                userName = member?.DisplayName;
                mentions.Add(userId, userName);
            }
            text = userName is not null or "" ? $"@{userName}" : "(unknown user)";
            linkUrl = SlackFormatter.UserUrl(organization.Domain, userId).ToString();
        }
        else if (element is UserGroupMention userGroupMention)
        {
            var userGroupId = userGroupMention.UserGroupId;
            // TODO: Need new scope for https://api.slack.com/methods/usergroups.list
            text = "(unknown group)";
            linkUrl = SlackFormatter.UserGroupUrl(organization.Domain, userGroupId).ToString();
        }
        else if (element is RichTextSection richTextSection)
        {
            await WriteRichTextSectionHtml(builder, organization, richTextSection, mentions);
            return;
        }
        else
        {
            Log.UnhandledElement(element.ToString());
            return;
        }

        if (text is not { Length: > 0 })
        {
            return;
        }

        var slackStyle = allowStyle && element is StyledElement styled ? styled.Style : null;

        text = HttpUtility.HtmlEncode(text);
        text = text.Replace("\n", "<br/>", StringComparison.Ordinal);

        if (slackStyle?.Bold == true)
        {
            cssStyle.Add("font-weight: bold");
        }
        if (slackStyle?.Italic == true)
        {
            cssStyle.Add("font-style: italic");
        }
        if (slackStyle?.Strike == true)
        {
            cssStyle.Add("text-decoration: line-through");
        }

        var styleAttributeValue = cssStyle.Any() ? $"{string.Join("; ", cssStyle)}" : null;
        var styleString = cssStyle.Any() ? $@" style=""{string.Join("; ", cssStyle)}""" : null;

        // Slack appears to never apply links to code
        if (slackStyle?.Code == true)
        {
            using (builder.AppendTag("code", styleAttributeValue, insideInlineTagContent: true))
            {
                builder.Append(text);
            }
        }
        else if (linkUrl is not null)
        {
            var attributes = new Dictionary<string, string>
            {
                ["href"] = linkUrl,
            };
            if (styleAttributeValue is not null)
            {
                attributes["style"] = styleAttributeValue;
            }
            using (builder.AppendTag("a", attributes, insideInlineTagContent: true))
            {
                builder.Append(text);
            }
        }
        else if (styleString is not null)
        {
            using (builder.AppendTag("span", styleAttributeValue, insideInlineTagContent: true))
            {
                builder.Append(text);
            }
        }
        else
        {
            builder.Append(text);
        }
    }

}

static partial class BlockKitToHtmlFormatterLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipped formatting unexpected element {Element}")]
    public static partial void UnhandledElement(this ILogger<BlockKitToHtmlFormatter> logger, string? element);
}

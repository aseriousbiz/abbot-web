using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Logging;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Integrations.HubSpot;

/// <summary>
/// Formats Blocks into a format suitable for HubSpot ticket messages.
/// </summary>
public class HubSpotFormatter
{
    static readonly ILogger<HubSpotFormatter> Log = ApplicationLoggerFactory.CreateLogger<HubSpotFormatter>();
    readonly ISlackResolver _slackResolver;

    public HubSpotFormatter(ISlackResolver slackResolver)
    {
        _slackResolver = slackResolver;
    }

    public async Task<string> FormatConversationMessageForTimelineEventAsync(ConversationMessage message)
    {
        return message.Blocks.Any()
            ? await FormatBlocksAsync(message.Organization, message.Blocks)
            : message.Text;
    }



    internal async Task<string> FormatBlocksAsync(Organization organization, IEnumerable<ILayoutBlock> blocks)
    {
        var mentions = new Dictionary<string, string?>();

        var builder = new StringBuilder();
        foreach (var block in blocks.OfType<RichTextBlock>())
        {
            foreach (var section in block.Elements.OfType<RichTextSection>())
            {
                foreach (var textElement in section.Elements)
                {
                    await FormatElementAsync(builder, textElement, organization, mentions);
                }
            }
        }

        return builder.ToString();
    }

    async Task FormatElementAsync(StringBuilder builder, IElement element, Organization organization, Dictionary<string, string?> mentions)
    {
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
        else
        {
            Log.UnhandledElement(element.ToString());
            return;
        }

        // Slack often puts newlines at the start of the element.
        // If that's happened, we can just append the newlines without styling.
        while (text is { Length: > 0 } && text[0] == '\n')
        {
            builder.Append('\n');
            text = text[1..];
        }

        if (text is not { Length: > 0 })
        {
            return;
        }

        var textStyle = element is StyledElement styled ? styled.Style : null;

        if (linkUrl is not null)
        {
            builder.Append('[');
        }

        if (textStyle?.Bold == true)
        {
            builder.Append("**");
        }

        if (textStyle?.Italic == true)
        {
            builder.Append('*');
        }

        if (textStyle?.Strike == true)
        {
            builder.Append("~~");
        }

        if (textStyle?.Code == true)
        {
            builder.Append('`');
        }

        builder.Append(text);

        if (textStyle?.Code == true)
        {
            builder.Append('`');
        }

        if (textStyle?.Strike == true)
        {
            builder.Append("~~");
        }

        if (textStyle?.Italic == true)
        {
            builder.Append('*');
        }

        if (textStyle?.Bold == true)
        {
            builder.Append("**");
        }

        if (linkUrl is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $"]({linkUrl})");
        }
    }
}

static partial class HubSpotFormatterLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipped formatting unexpected element {Element}")]
    public static partial void UnhandledElement(this ILogger<HubSpotFormatter> logger, string? element);
}

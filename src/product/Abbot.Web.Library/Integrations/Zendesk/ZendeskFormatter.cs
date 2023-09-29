using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Models;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Integrations.Zendesk;

public class ZendeskFormatter
{
    readonly ConversationMessageToHtmlFormatter _conversationMessageToHtmlFormatter;
    readonly IUrlGenerator _urlGenerator;

    public ZendeskFormatter(
        ConversationMessageToHtmlFormatter conversationMessageToHtmlFormatter,
        IUrlGenerator urlGenerator)
    {
        _conversationMessageToHtmlFormatter = conversationMessageToHtmlFormatter;
        _urlGenerator = urlGenerator;
    }

    // We don't want to be coupled to this being static in the future.
    // If we change the comment format, we might need the IUrlGenerator, for example.
    public async Task<Comment> CreateCommentAsync(Conversation conversation, ConversationMessage message, long authorId)
    {
        var body = await _conversationMessageToHtmlFormatter.FormatMessageAsHtmlAsync(
            message,
            conversation.Organization);

        return new Comment
        {
            AuthorId = authorId,
            HtmlBody = body,
        };
    }

    public ZendeskTicket CreateTicket(
        Conversation conversation,
        long requesterId,
        string subject,
        IReadOnlyDictionary<string, object?> fields,
        Member actor,
        long? organizationId)
    {
        var conversationUrl = _urlGenerator.ConversationDetailPage(conversation.Id);
        var slackMessageUrl = conversation.GetFirstMessageUrl();
        var userUrl = actor.FormatPlatformUrl();

        var ticket = new ZendeskTicket
        {
            Subject = subject,
            ExternalId = conversationUrl.ToString(),
            RequesterId = requesterId,
            OrganizationId = organizationId,
        };

        // Bind form fields
        BindFields(ticket, fields);

        // Special handling for the comment field
        var description = fields.GetValueOrDefault("comment") ?? string.Empty;

        ticket.Comment = new Comment
        {
            HtmlBody =
                $"""
                {description}
                <p style="margin-top: 2rem">
                    <em>
                        Created by <a href="{userUrl}">{actor.DisplayName}</a> from this <a href="{slackMessageUrl}">Slack thread</a>.
                        &bull; <a href="{conversationUrl}">View on ab.bot</a>
                    </em>
                </p>
                """
        };

        return ticket;
    }

    static void BindFields(ZendeskTicket zendeskTicket, IReadOnlyDictionary<string, object?> fields)
    {
        foreach (var (key, value) in fields)
        {
            if (value is null or "")
            {
                continue;
            }

            switch (key)
            {
                case "tags":
                    // Value is comma-separated
                    var valueText = value.ToString();
                    if (valueText is null)
                    {
                        continue;
                    }
                    var tags = valueText.Split(',').Select(t => t.Trim()).ToList();
                    zendeskTicket.Tags = tags;
                    break;
                case "type":
                    zendeskTicket.Type = value.ToString();
                    break;
                case "priority":
                    zendeskTicket.Priority = value.ToString();
                    break;
                case var x when x.StartsWith("custom_field:", StringComparison.OrdinalIgnoreCase):
                    var fieldId = long.Parse(x["custom_field:".Length..], CultureInfo.InvariantCulture);
                    zendeskTicket.CustomFields.Add(new(fieldId, value));
                    break;
            }
        }
    }
}

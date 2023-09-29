using System.Collections.Generic;
using System.Linq;
using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Renders Slack messages for Conversations and Conversation Events in the Hub.
/// </summary>
public class HubMessageRenderer
{
    readonly IUserRepository _userRepository;
    readonly IPublishEndpoint _publishEndpoint;
    readonly IUrlGenerator _urlGenerator;

    public HubMessageRenderer(
        IUserRepository userRepository,
        IPublishEndpoint publishEndpoint,
        IUrlGenerator urlGenerator)
    {
        _userRepository = userRepository;
        _publishEndpoint = publishEndpoint;
        _urlGenerator = urlGenerator;
    }

    public async Task UpdateHubMessageAsync(Conversation conversation)
    {
        await _publishEndpoint.Publish(new RefreshHubMessage()
        {
            OrganizationId = conversation.Organization,
            ConversationId = conversation,
        });
    }

    /// <summary>
    /// Renders a <see cref="MessageRequest"/> to represent the root of a Hub Thread.
    /// The request is not yet ready to be sent to Slack, as it does not contain the
    /// destination channel.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> to generate the message for.</param>
    /// <param name="responders">A list of <see cref="Member"/>s representing the responders for this conversation.</param>
    /// <returns></returns>
    public async Task<MessageRequest> RenderHubThreadRootAsync(
        Conversation conversation)
    {
        var responders = await GetRespondersAsync(conversation);

        static string FormatTime(DateTime utcTime) => SlackFormatter.FormatTime(utcTime);

        var blocks = new List<ILayoutBlock>();
        blocks.Add(new Section(new MrkdwnText($"""
            Conversation with {conversation.StartedBy.ToSilentMention()} in {conversation.Room.ToMention()}
            """)));
        blocks.Add(new Context(new MrkdwnText($"""
            *Created:* {FormatTime(conversation.Created)}. *Last updated*: {FormatTime(conversation.LastMessagePostedOn)}
            """)));

        if ((conversation.Properties.Summary ?? conversation.Summary) is { Length: > 0 } summary)
        {
            blocks.Add(new Section(new MrkdwnText($"""
                *Summary*:
                > {summary}
                """)));
        }

        var responderList = string.Join(" ", responders.Select(r => r.ToSilentMention()));
        var respondersMsg = responderList.Length > 0
            ? responderList
            : "_None Assigned_";

        blocks.Add(new Section(
            new MrkdwnText($"""
            *Responders*
            {respondersMsg}
            """)));

        if (conversation.Properties.Conclusion is { Length: > 0 } conclusion)
        {
            blocks.Add(new Context(new MrkdwnText($"""
                *Suggested Next Step:* {conclusion}
                """)));
        }

        var tagList = conversation.Tags
            .Where(TagRepository.VisibleTagsFilter)
            .Select(t => $"`{t.Tag.Name}`");

        var tags = string.Join(" ", tagList);
        if (tags.Length > 0)
        {
            blocks.Add(new Context(new MrkdwnText($"""
            :label: {string.Join(" ", tagList)}
            """)));
        }

        // Determine the available actions
        blocks.Add(new Actions(
            new ButtonElement("Open", "open", conversation.GetFirstMessageUrl())));

        return new MessageRequest()
        {
            Blocks = blocks,
            Text = $"A new conversation was posted by {conversation.StartedBy.DisplayName} in {conversation.Room.Name}",
        };
    }

    async Task<IReadOnlyList<Member>> GetRespondersAsync(Conversation conversation)
    {
        if (conversation.Assignees is { Count: > 0 } assignees)
        {
            return assignees;
        }

        if (conversation.Room.GetFirstResponders().ToList() is { Count: > 0 } frs)
        {
            return frs;
        }

        return await _userRepository.GetDefaultFirstRespondersAsync(conversation.Organization);
    }
}

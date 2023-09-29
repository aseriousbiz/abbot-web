using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging.Slack;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Messaging;

/// <summary>
/// A builder for creating the blocks that go into an Open Ticket message.
/// </summary>
public class OpenTicketMessageBuilder
{
    readonly IIntegrationRepository _integrationRepository;

    public OpenTicketMessageBuilder(IIntegrationRepository integrationRepository)
    {
        _integrationRepository = integrationRepository;
    }

    /// <summary>
    /// Returns the blocks that go into an Open Ticket message.
    /// </summary>
    /// <param name="conversation">The existing <see cref="Conversation"/>, if any.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="messageId">The message Id.</param>
    /// <param name="organization">The organization.</param>
    /// <returns>The set of blocks to send.</returns>
    public async Task<IEnumerable<ILayoutBlock>> BuildOpenTicketMessageBlocksAsync(
        Conversation? conversation,
        string? channel,
        string? messageId,
        Organization organization)
    {
        // We have to pop up an ephemeral message to ask the user to click a button to open the modal.
        // First though, we need to figure out what integrations are enabled.
        // We don't need to create a conversation first because the Ticket Handler will create the Conversation
        // if there's no conversation.
        var actionButtons = new List<IActionElement>();

        var ticketingIntegrations = await _integrationRepository.GetTicketingIntegrationsAsync(organization);

        var conversationIdentifier = new ConversationIdentifier(channel, messageId, conversation?.Id);
        foreach (var (ticketing, settings) in ticketingIntegrations)
        {
            if (!ticketing.Enabled)
            {
                continue;
            }

            if (settings.FindLink(conversation, ticketing) is not null)
            {
                continue;
            }

            actionButtons.Add(
                new ButtonElement($"Create {settings.IntegrationName} Ticket", Value: conversationIdentifier)
                {
                    ActionId = CreateTicketFormModal.For(ticketing),
                });
        }

        if (actionButtons.Count is 0)
        {
            return Enumerable.Empty<ILayoutBlock>();
        }

        // Ok, but if we do have actions, let's also add a dismiss button
        actionButtons.Add(CommonBlockKitElements.DismissButton());

        var thisConversation = conversation?.GetFirstMessageUrl() is { } firstMessageUrl
            ? new Hyperlink(firstMessageUrl, "this conversation").ToString()
            : "this conversation";

        return new ILayoutBlock[] {
            new Section(
                new MrkdwnText(
                    $"Please select an action to take on {thisConversation}.")),
            new Actions(blockId: InteractionCallbackInfo.For<ReactionHandler>(), elements: actionButtons.ToArray())
        };
    }
}

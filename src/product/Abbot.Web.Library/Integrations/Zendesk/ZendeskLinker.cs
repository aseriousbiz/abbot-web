using System.Collections.Generic;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Integrations.Zendesk;

public class ZendeskLinker : ITicketLinker<ZendeskSettings, ZendeskTicket>
{
    static readonly ILogger<ZendeskLinker> Log =
        ApplicationLoggerFactory.CreateLogger<ZendeskLinker>();

    readonly IConversationRepository _conversationRepository;
    readonly IZendeskClientFactory _clientFactory;
    readonly IZendeskResolver _zendeskResolver;
    readonly ZendeskFormatter _zendeskFormatter;
    readonly IMessageRenderer _messageRenderer;
    readonly ISlackThreadExporter _slackThreadExporter;
    readonly IBackgroundJobClient _jobClient;
    readonly IClock _clock;

    public ZendeskLinker(
        IConversationRepository conversationRepository,
        IZendeskClientFactory clientFactory,
        IZendeskResolver zendeskResolver,
        ZendeskFormatter zendeskFormatter,
        IMessageRenderer messageRenderer,
        ISlackThreadExporter slackThreadExporter,
        IBackgroundJobClient jobClient,
        IClock clock)
    {
        _conversationRepository = conversationRepository;
        _clientFactory = clientFactory;
        _zendeskResolver = zendeskResolver;
        _zendeskFormatter = zendeskFormatter;
        _messageRenderer = messageRenderer;
        _slackThreadExporter = slackThreadExporter;
        _jobClient = jobClient;
        _clock = clock;
    }

    public async Task<ZendeskTicket?> CreateTicketAsync(
        Integration integration,
        ZendeskSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor)
    {
        var organization = conversation.Organization;
        var subject = properties["subject"].Require<string>();
        var organizationLink = ZendeskOrganizationLink.Parse(
            (string?)properties.GetValueOrDefault("organizationLink"));

        using var __ = Log.BeginZendeskOrganizationScope(organizationLink);

        var client = _clientFactory.CreateClient(settings);

        // Before we do anything, let's resolve the conversation author's Zendesk account.
        var zendeskUser = await _zendeskResolver.ResolveZendeskIdentityAsync(
            client,
            organization,
            conversation.StartedBy,
            organizationLink?.OrganizationId);

        if (zendeskUser is null)
        {
            throw new TicketConfigurationException(
                "I couldn't create the necessary Zendesk users for this conversation.",
                TicketErrorReason.UserConfiguration);
        }
        using var _ = Log.BeginZendeskUserScope(zendeskUser);

        // Cleanup description.
        if (properties.TryGetValue("comment", out var description))
        {
            // Render the message as HTML.
            properties = new Dictionary<string, object?>(properties)
            {
                ["comment"] = (await _messageRenderer.RenderMessageAsync(description?.ToString(), conversation.Organization))
                    .ToHtml(),
            };
        }

        // We can only set the organization ID if the user is a member of that organization.
        // TODO: Track multiple organization memberships.

        // So, we only use the linked organization ID if the user we retrieved is _specifically_ associated with that organization.
        var ticketOrganizationId =
            organizationLink?.OrganizationId is { } ticketOrgId && ticketOrgId == zendeskUser.OrganizationId
                ? (long?)ticketOrgId
                : null;

        var ticketBody = _zendeskFormatter.CreateTicket(
            conversation,
            zendeskUser.Id,
            subject,
            properties,
            actor,
            ticketOrganizationId);

        var response = await client.CreateTicketAsync(new TicketMessage
        {
            Body = ticketBody
        });
        return response.Body;
    }

    public async Task<ConversationLink?> CreateConversationLinkAsync(
        Integration integration,
        ZendeskSettings settings,
        ZendeskTicket ticket,
        Conversation conversation,
        Member actor)
    {
        var org = conversation.Organization;
        var room = conversation.Room;

        // We retrieve the Slack thread from the Slack API here and store it in the database.
        // This is so we can later import the thread into Zendesk without running into a race
        // condition that's caused by retrieving the thread from the Slack API later.
        // This doesn't solve the ordering issue where a comment created after linking, but before
        // the import will not be tacked on the end of the Zendesk thread.
        var threadSetting = await _slackThreadExporter.ExportThreadAsync(
            conversation.FirstMessageId,
            room.PlatformRoomId,
            org,
            actor);

        // Link the conversation to that ticket
        var linkedConversation = await _conversationRepository.CreateLinkAsync(
            conversation,
            ConversationLinkType.ZendeskTicket,
            ticket.Url,
            actor,
            _clock.UtcNow);

        if (threadSetting is not null)
        {
            _jobClient.Enqueue<ZendeskLinkedConversationThreadImporter>(
                importer => importer.ImportThreadAsync(linkedConversation, threadSetting.Name));
        }

        return linkedConversation;
    }
}

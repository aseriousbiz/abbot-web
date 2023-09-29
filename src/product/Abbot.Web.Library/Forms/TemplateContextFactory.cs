using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Forms;

/// <summary>
/// A factory for creating a <see cref="CreateTicketTemplateContext" /> which can be bound to using a
/// <see cref="FormDefinition"/> in order to populate a Create Ticket modal for a ticket integration such as
/// Zendesk (<see cref="CreateZendeskTicketFormModal"/>) or Hubspot (<see cref="CreateHubSpotTicketFormModal"/>).
/// </summary>
public interface ITemplateContextFactory
{
    /// <summary>
    /// Creates a <see cref="CreateTicketTemplateContext" /> from the specified <paramref name="conversation"/> and
    /// <paramref name="actor"/>.
    /// </summary>
    /// <param name="conversation">The conversation to create a ticket for.</param>
    /// <param name="actor">The actor creating the ticket.</param>
    /// <returns></returns>
    public Task<CreateTicketTemplateContext> CreateTicketTemplateContextAsync(Conversation conversation, Member actor);
}

public class TemplateContextFactory : ITemplateContextFactory
{
    readonly IMessageRenderer _messageRenderer;
    readonly IUrlGenerator _urlGenerator;
    readonly IMetadataRepository _metadataRepository;

    public TemplateContextFactory(
        IMessageRenderer messageRenderer,
        IUrlGenerator urlGenerator,
        IMetadataRepository metadataRepository)
    {
        _messageRenderer = messageRenderer;
        _urlGenerator = urlGenerator;
        _metadataRepository = metadataRepository;
    }

    public async Task<CreateTicketTemplateContext> CreateTicketTemplateContextAsync(Conversation conversation, Member actor)
    {
        var conversationUrl = _urlGenerator.ConversationDetailPage(conversation.Id);
        var renderedMessage = await _messageRenderer.RenderMessageAsync(conversation.Title, conversation.Organization);
        var plainTextTitle = renderedMessage.ToText();
        var roomMetadata = await _metadataRepository.ResolveValuesForRoomAsync(conversation.Room);
        var customer = conversation.Room.Customer;
        var customerMetadata = customer is not null
            ? await _metadataRepository.ResolveValuesForCustomerAsync(customer)
            : new Dictionary<string, string?>();

        return new CreateTicketTemplateContext(
            ConversationTemplateModel.FromConversation(conversation, conversationUrl, plainTextTitle),
            RoomTemplateModel.FromRoom(conversation.Room, roomMetadata),
            OrganizationTemplateModel.FromOrganization(conversation.Organization),
            MemberTemplateModel.FromMember(actor),
            CustomerTemplateModel.FromCustomer(customer, customerMetadata));
    }
}

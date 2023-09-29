using System.Collections.Generic;
using System.Globalization;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Routing;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations.HubSpot;

public class CreateHubSpotTicketFormModal : IHandler
{
    readonly TicketModalService _ticketModalService;
    readonly IUrlGenerator _urlGenerator;
    readonly ITemplateContextFactory _templateContextFactory;

    public CreateHubSpotTicketFormModal(
        TicketModalService ticketModalService,
        IUrlGenerator urlGenerator,
        ITemplateContextFactory templateContextFactory)
    {
        _ticketModalService = ticketModalService;
        _urlGenerator = urlGenerator;
        _templateContextFactory = templateContextFactory;
    }

    static string FormKey => SystemForms.CreateHubSpotTicket;

    /// <summary>
    /// Handle a click from the "Create HubSpot Ticket" button from any modal that routes to this handler.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
        => await _ticketModalService.OnInteractionAsync(viewContext, IntegrationType.HubSpot, CreateAsync);

    /// <summary>
    /// Handle a click from the "Create HubSpot Ticket" button from buttons in a message that routes to this handler.
    /// </summary>
    /// <param name="platformMessage">Information about the message that was interacted with.</param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
        => await _ticketModalService.OnMessageInteractionAsync(platformMessage, IntegrationType.HubSpot, CreateAsync);

    /// <summary>
    /// Handles the event raised when the modal view is submitted.
    /// </summary>
    /// <param name="viewContext">Information about the view that was submitted.</param>
    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        // Load the conversation
        var privateMetadata = PrivateMetadata.Parse(viewContext.Payload.View.PrivateMetadata).Require();
        var roomIntegrationLink = privateMetadata.RoomIntegrationLink?.ToString();

        await _ticketModalService.OnSubmissionAsync<HubSpotSettings>(
            viewContext,
            FormKey,
            privateMetadata.ConversationId,
            ("roomIntegrationLink", roomIntegrationLink));
    }

    public async Task<ViewUpdatePayload?> CreateAsync(TicketingIntegration ticketing, Conversation convo, Member actor)
    {
        var definition = await _ticketModalService.GetFormDefinitionAsync(convo.Organization, FormKey);

        var ticketContext = await _templateContextFactory.CreateTicketTemplateContextAsync(convo, actor);

        // Populate view state
        var state = new ViewState(
            ticketing,
            ticketContext,
            convo,
            actor.CanManageConversations(),
            convo.Room.GetIntegrationLink(ticketing) ?? new(),
            _urlGenerator.RoomSettingsPage(convo.Room),
            definition);

        return Render(state);
    }

    ViewUpdatePayload Render(ViewState state)
    {
        if (!_ticketModalService.TryTranslateForm(FormKey, state.FormDefinition, state.Context, out var blocks))
        {
            return AlertModal.Render("The form definition is invalid. Please contact support.", "Internal Error");
        }

        var companyLink = state.IntegrationRoomLink.ToSlackLink(state.CanManageConversations)
            ?? (state.CanManageConversations
                ? $"_No Company Linked_ <{state.RoomSettingsLink}#hubspot|Change>"
                : "_No Company Linked_");

        blocks.Insert(0,
            new Section(
                new MrkdwnText("*Requester*"),
                new MrkdwnText("*Company*"),
                new MrkdwnText(state.Conversation.StartedBy.User.ToMention()),
                new MrkdwnText(companyLink))
            {
                BlockId = "info_section"
            });

        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<CreateHubSpotTicketFormModal>($"{state.Ticketing.Integration.Id}"),
            Title = "Create HubSpot Ticket",
            Close = "Cancel",
            Submit = "Create",
            Blocks = blocks,
            PrivateMetadata = new PrivateMetadata(state.Conversation, state.IntegrationRoomLink.Link),
        };

        return payload;
    }

    record ViewState(
        TicketingIntegration Ticketing,
        CreateTicketTemplateContext Context,
        Conversation Conversation,
        bool CanManageConversations,
        IntegrationRoomLink IntegrationRoomLink,
        Uri RoomSettingsLink,
        FormDefinition FormDefinition);

    public record PrivateMetadata(Id<Conversation> ConversationId, IntegrationLink? RoomIntegrationLink)
        : PrivateMetadataBase
    {
        public static PrivateMetadata? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 2, out var parts)
                ? new(
                    Id<Conversation>.Parse(parts[0], CultureInfo.InvariantCulture),
                    HubSpotCompanyLink.Parse(parts[1]))
                // TODO: Remove once deployed; don't want to break tickets in progress
                : TrySplitParts(privateMetadata, 1, out var oldParts)
                ? new(
                    Id<Conversation>.Parse(oldParts[0], CultureInfo.InvariantCulture),
                    null)
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return ConversationId.ToStringInvariant();
            yield return RoomIntegrationLink?.ToString() ?? string.Empty;
        }

        public override string ToString() => base.ToString();
    }
}

using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Routing;
using Serious.Abbot.Skills;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations.Zendesk;

public class CreateZendeskTicketFormModal : IHandler
{
    readonly TicketModalService _ticketModalService;
    readonly IUrlGenerator _urlGenerator;
    readonly ITemplateContextFactory _templateContextFactory;

    public CreateZendeskTicketFormModal(
        TicketModalService ticketModalService,
        IUrlGenerator urlGenerator,
        ITemplateContextFactory templateContextFactory)
    {
        _ticketModalService = ticketModalService;
        _urlGenerator = urlGenerator;
        _templateContextFactory = templateContextFactory;
    }

    static string FormKey => SystemForms.CreateZendeskTicket;

    /// <summary>
    /// Handle a click from the "Create Zendesk Ticket" button from any modal that routes to this handler.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
        => await _ticketModalService.OnInteractionAsync(viewContext, IntegrationType.Zendesk, CreateAsync);

    /// <summary>
    /// Handle a click from the "Create Zendesk Ticket" button from buttons in a message that routes to this handler.
    /// </summary>
    /// <param name="platformMessage">Information about the message that was interacted with.</param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
        => await _ticketModalService.OnMessageInteractionAsync(platformMessage, IntegrationType.Zendesk, CreateAsync);

    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        // Load the conversation
        var privateMetadata = PrivateMetadata.Parse(viewContext.Payload.View.PrivateMetadata).Require();
        var organizationLink = privateMetadata.RoomIntegrationLink?.ToString();

        // Pull the hard-coded form field ("subject") out of the state.
        var state = viewContext.Payload.View.State.Require();
        var subject = state.RequireValue("subject_input");

        await _ticketModalService.OnSubmissionAsync<ZendeskSettings>(
            viewContext,
            FormKey,
            privateMetadata.ConversationId,
            ("subject", subject),
            ("organizationLink", organizationLink));
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
        var organizationLink = state.IntegrationRoomLink.ToSlackLink(state.CanManageConversations)
            ?? (state.CanManageConversations
                ? $"_No Organization Linked_ <{state.RoomSettingsLink}#zendesk|Change>"
                : "_No Organization Linked_");

        var blocks = new List<ILayoutBlock>
        {
            new Input(
                "Subject",
                new PlainTextInput
                {
                    Placeholder = "Ticket Subject",
                    InitialValue = "", // Default is blank: but we should evaluate the form definition for subject.
                },
                "subject_input"),
            new Section(
                new MrkdwnText("*Requester*"),
                new MrkdwnText("*Organization*"),
                new MrkdwnText(state.Conversation.StartedBy.User.ToMention()),
                new MrkdwnText(organizationLink))
            {
                BlockId = "info_section"
            },
        };

        if (!_ticketModalService.TryTranslateForm(FormKey, state.FormDefinition, state.Context, out var formBlocks))
        {
            return AlertModal.Render("The form definition is invalid. Please contact support.", "Internal Error");
        }
        blocks.AddRange(formBlocks);

        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<CreateZendeskTicketFormModal>($"{state.Ticketing.Integration.Id}"),
            Title = "Create Zendesk Ticket",
            Close = "Cancel",
            Submit = "Create",
            Blocks = blocks,
            PrivateMetadata = new PrivateMetadata(state.Conversation, state.IntegrationRoomLink.Link),
        };

        return payload;
    }

    public record PrivateMetadata(Id<Conversation> ConversationId, IntegrationLink? RoomIntegrationLink)
        : PrivateMetadataBase
    {
        public static PrivateMetadata? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 2, out var parts)
                ? new PrivateMetadata(
                    Id<Conversation>.Parse(parts[0], CultureInfo.InvariantCulture),
                    ZendeskOrganizationLink.Parse(parts[1]))
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return ConversationId.ToStringInvariant();
            yield return RoomIntegrationLink?.ToString() ?? string.Empty;
        }

        public override string ToString() => base.ToString();
    }

    record ViewState(
        TicketingIntegration Ticketing,
        CreateTicketTemplateContext Context,
        Conversation Conversation,
        bool CanManageConversations,
        IntegrationRoomLink IntegrationRoomLink,
        Uri RoomSettingsLink,
        FormDefinition FormDefinition);
}

static partial class CreateZendeskTicketFormModalLoggingExtensions
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Room {PlatformRoomId} has multiple linked Zendesk organizations")]
    public static partial void RoomHasMultipleZendeskOrganizations(this ILogger<CreateZendeskTicketFormModal> logger,
        string platformRoomId);
}

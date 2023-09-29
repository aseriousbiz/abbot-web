using System.Collections.Generic;
using System.Globalization;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations.MergeDev;

public class CreateMergeDevTicketFormModal : IHandler
{
    readonly TicketModalService _ticketModalService;
    readonly ITemplateContextFactory _templateContextFactory;

    public CreateMergeDevTicketFormModal(
        TicketModalService ticketModalService,
        ITemplateContextFactory templateContextFactory)
    {
        _ticketModalService = ticketModalService;
        _templateContextFactory = templateContextFactory;
    }

    static string FormKey => SystemForms.CreateGenericTicket;

    /// <summary>
    /// Handle a click from the "Create Ticket" button from any modal that routes to this handler.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        await _ticketModalService.OnInteractionAsync(viewContext, IntegrationType.Ticketing, CreateAsync);
    }

    /// <summary>
    /// Handle a click from the "Create Ticket" button from buttons in a message that routes to this handler.
    /// </summary>
    /// <param name="platformMessage">Information about the message that was interacted with.</param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        await _ticketModalService.OnMessageInteractionAsync(platformMessage, IntegrationType.Ticketing, CreateAsync);
    }

    /// <summary>
    /// Handles the event raised when the modal view is submitted.
    /// </summary>
    /// <param name="viewContext">Information about the view that was submitted.</param>
    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var privateMetadata = PrivateMetadata.Parse(viewContext.Payload.View.PrivateMetadata).Require();

        await _ticketModalService.OnSubmissionAsync<TicketingSettings>(
            viewContext,
            FormKey,
            privateMetadata.ConversationId);
    }

    public async Task<ViewUpdatePayload?> CreateAsync(TicketingIntegration ticketing, Conversation convo, Member actor)
    {
        var definition = await _ticketModalService.GetFormDefinitionAsync(convo.Organization, FormKey);

        var ticketContext = await _templateContextFactory.CreateTicketTemplateContextAsync(convo, actor);

        // Populate view state
        var state = new ViewState(ticketing, ticketContext, convo, definition);

        return Render(state);
    }

    ViewUpdatePayload Render(ViewState state)
    {
        if (!_ticketModalService.TryTranslateForm(FormKey, state.FormDefinition, state.Context, out var blocks))
        {
            return AlertModal.Render("The form definition is invalid. Please contact support.", "Internal Error");
        }

        blocks.Insert(0, new Context($"Create ticket in {state.Ticketing.Settings.IntegrationName}."));
        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<CreateMergeDevTicketFormModal>($"{state.Ticketing.Integration.Id}"),
            Title = $"Create Ticket",
            Close = "Cancel",
            Submit = "Create",
            Blocks = blocks,
            PrivateMetadata = new PrivateMetadata(state.Conversation),
        };

        return payload;
    }

    public record PrivateMetadata(Id<Conversation> ConversationId)
        : PrivateMetadataBase
    {
        public static PrivateMetadata? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 1, out var parts)
                ? new PrivateMetadata(
                    Id<Conversation>.Parse(parts[0], CultureInfo.InvariantCulture))
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return ConversationId.ToStringInvariant();
        }

        public override string ToString() => base.ToString();
    }

    record ViewState(
        TicketingIntegration Ticketing,
        CreateTicketTemplateContext Context,
        Conversation Conversation,
        FormDefinition FormDefinition);
}

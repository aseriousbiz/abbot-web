using System.Collections.Generic;
using System.Globalization;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Routing;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations.GitHub;

public class CreateGitHubIssueFormModal : IHandler
{
    readonly TicketModalService _ticketModalService;
    readonly IUrlGenerator _urlGenerator;
    readonly ITemplateContextFactory _templateContextFactory;

    public CreateGitHubIssueFormModal(
        TicketModalService ticketModalService,
        IUrlGenerator urlGenerator,
        ITemplateContextFactory templateContextFactory)
    {
        _ticketModalService = ticketModalService;
        _urlGenerator = urlGenerator;
        _templateContextFactory = templateContextFactory;
    }

    static string FormKey => SystemForms.CreateGitHubIssue;

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
        await _ticketModalService.OnSubmissionAsync<GitHubSettings>(
            viewContext,
            FormKey,
            privateMetadata.ConversationId);
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
            ConversationTracker.IsSupportee(actor, convo.Room),
            _urlGenerator.RoomSettingsPage(convo.Room),
            definition);

        return Render(state);
    }

    ViewUpdatePayload Render(ViewState state)
    {
        var blocks = new List<ILayoutBlock>();

        if (!_ticketModalService.TryTranslateForm(FormKey, state.FormDefinition, state.Context, out var formBlocks))
        {
            return AlertModal.Render("The form definition is invalid. Please contact support.", "Internal Error");
        }
        blocks.AddRange(formBlocks);

        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<CreateGitHubIssueFormModal>($"{state.Ticketing.Integration.Id}"),
            Title = "Create GitHub Issue",
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
        bool IsSupportee,
        Uri RoomSettingsLink,
        FormDefinition FormDefinition);
}

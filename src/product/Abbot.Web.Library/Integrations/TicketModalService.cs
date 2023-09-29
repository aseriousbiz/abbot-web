using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations;

public class TicketModalService
{
    static readonly ILogger<TicketModalService> Log =
        ApplicationLoggerFactory.CreateLogger<TicketModalService>();

    readonly IConversationRepository _conversationRepository;
    readonly IConversationThreadResolver _conversationThreadResolver;
    readonly IConversationTracker _conversationTracker;
    readonly IFormEngine _formEngine;
    readonly IFormsRepository _formsRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IRoomRepository _roomRepository;
    readonly ITemplateContextFactory _templateContextFactory;
    readonly DismissHandler _dismissHandler;
    readonly ITicketIntegrationService _ticketService;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public TicketModalService(
        IConversationRepository conversationRepository,
        IConversationThreadResolver conversationThreadResolver,
        IConversationTracker conversationTracker,
        IFormEngine formEngine,
        IFormsRepository formsRepository,
        IIntegrationRepository integrationRepository,
        IRoomRepository roomRepository,
        ITemplateContextFactory templateContextFactory,
        DismissHandler dismissHandler,
        ITicketIntegrationService ticketService,
        IAnalyticsClient analyticsClient,
        IClock clock)
    {
        _conversationRepository = conversationRepository;
        _conversationThreadResolver = conversationThreadResolver;
        _conversationTracker = conversationTracker;
        _formEngine = formEngine;
        _formsRepository = formsRepository;
        _integrationRepository = integrationRepository;
        _roomRepository = roomRepository;
        _templateContextFactory = templateContextFactory;
        _dismissHandler = dismissHandler;
        _ticketService = ticketService;
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    /// <summary>
    /// Handle a click from the "Create Ticket" button from any modal that routes to this handler.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    /// <param name="integrationType">The expected <see cref="IntegrationType"/>.</param>
    /// <param name="createModalView">Method to create the Create Ticket modal.</param>
    public async Task OnInteractionAsync(
        IViewContext<IViewBlockActionsPayload> viewContext,
        IntegrationType integrationType,
        Func<TicketingIntegration, Conversation, Member, Task<ViewUpdatePayload?>> createModalView)
    {
        var payloadAction = RequireSingleAction(viewContext);
        await CreateModalToHandleCreateButtonClickAsync(
            payloadAction,
            viewContext.FromMember,
            viewContext.Organization,
            integrationType,
            createModalView,
            async modal => await viewContext.PushModalViewAsync(modal));
    }

    /// <summary>
    /// Extracts a single <see cref="IPayloadElement"/> from <paramref name="viewContext"/>.
    /// </summary>
    /// <param name="viewContext">The view context.</param>
    public static IPayloadElement RequireSingleAction(IViewContext<IViewBlockActionsPayload> viewContext) =>
        viewContext.Payload.Actions.Single().Require<IValueElement>();

    /// <summary>
    /// Handle a click from the "Create Ticket" button from buttons in a message that routes to this handler.
    /// </summary>
    /// <param name="platformMessage">Information about the message that was interacted with.</param>
    /// <param name="integrationType">The expected <see cref="IntegrationType"/>.</param>
    /// <param name="createModalView">Method to create the Create Ticket modal.</param>
    public async Task OnMessageInteractionAsync(
        IPlatformMessage platformMessage,
        IntegrationType integrationType,
        Func<TicketingIntegration, Conversation, Member, Task<ViewUpdatePayload?>> createModalView)
    {
        var payloadAction = RequireSingleAction(platformMessage);
        await CreateModalToHandleCreateButtonClickAsync(
            payloadAction,
            platformMessage.From,
            platformMessage.Organization,
            integrationType,
            createModalView,
            async modal => {
                await platformMessage.Responder.OpenModalAsync(platformMessage.TriggerId.Require(), modal);
                await _dismissHandler.OnMessageInteractionAsync(platformMessage);
            });
    }

    /// <summary>
    /// Extracts a single <see cref="IPayloadElement"/> from <paramref name="platformMessage"/>.
    /// </summary>
    /// <param name="platformMessage">The platform message.</param>
    public static IPayloadElement RequireSingleAction(IPlatformMessage platformMessage) =>
        platformMessage.Payload.InteractionInfo.Require().ActionElement.Require();

    async Task CreateModalToHandleCreateButtonClickAsync(
        IPayloadElement payloadAction,
        Member actor,
        Organization organization,
        IntegrationType integrationType,
        Func<TicketingIntegration, Conversation, Member, Task<ViewUpdatePayload?>> createModalView,
        Func<ViewUpdatePayload, Task> openModal)
    {
        var ticketing = await GetTicketing(organization, integrationType, payloadAction);

        var actionValue = payloadAction.Require<IValueElement>().Value.Require();
        // Pull the encoded out of the value. It's either a conversation Id or a channel:messageId combo.
        var (channel, messageId, conversationId) = ConversationIdentifier.Parse(actionValue);

        var conversation = conversationId != null
            ? await _conversationRepository.GetConversationAsync(conversationId.Value).Require()
            : await TrackConversationAsync(
                actor,
                channel.Require(),
                messageId.Require(),
                organization);

        var modal = await createModalView(ticketing, conversation, actor).Require();

        await openModal(modal);

        _analyticsClient.Screen(
            "Ticket Modal",
            AnalyticsFeature.Integrations,
            actor,
            organization,
            new {
                integration = ticketing.Settings.AnalyticsSlug,
            });
    }

    async Task<TicketingIntegration> GetTicketing(Organization organization, IntegrationType integrationType, IPayloadElement action)
    {
        Expect.True(CallbackInfo.TryParseAs<InteractionCallbackInfo>(action.ActionId, out var callbackInfo));

        // TODO: Remove once we're confident all callbacks include IntegrationId
        if (!Id<Integration>.TryParse(callbackInfo.ContextId, out var integrationId))
        {
            var integration = await _integrationRepository.GetIntegrationAsync(organization, integrationType).Require();
            Expect.True(_integrationRepository.TryGetTicketingSettings(integration, out var settings));
            return new(integration, settings);
        }

        return await _integrationRepository.GetTicketingIntegrationByIdAsync(organization, integrationId).Require();
    }

    public async Task<Conversation> TrackConversationAsync(
        Member actor,
        string channel,
        string messageId,
        Organization organization)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(channel, organization);
        if (room is null)
        {
            throw new InvalidOperationException($"Room {channel} not found for Organization {organization.Id}.");
        }

        var messages = await _conversationThreadResolver.ResolveConversationMessagesAsync(
            room,
            messageId);
        var conversation = await _conversationTracker.CreateConversationAsync(
            messages,
            actor,
            _clock.UtcNow);
        return conversation
               ?? throw new InvalidOperationException($"Failed to create Conversation for message {messageId} in Room {room.Id} for Organization {organization.Id}.");
    }

    public async Task<FormDefinition> GetFormDefinitionAsync(Organization organization, string formKey)
    {
        var form = (await _formsRepository.GetFormAsync(organization, formKey));
        var definition = form is { Enabled: true }
            ? FormEngine.DeserializeFormDefinition(form.Definition)
            : SystemForms.Definitions[formKey];
        return definition;
    }

    public async Task OnSubmissionAsync<TSettings>(
        IViewContext<IViewSubmissionPayload> viewContext,
        string formKey,
        Id<Conversation> conversationId,
        params (string, object?)[] additionalProperties)
        where TSettings : class, ITicketingSettings
    {
        Expect.True(CallbackInfo.TryParseAs<InteractionCallbackInfo>(viewContext.Payload.View.CallbackId, out var callbackInfo));
        var integrationId = Id<Integration>.Parse(callbackInfo.ContextId, CultureInfo.InvariantCulture);

        var convo = await _conversationRepository.GetConversationAsync(conversationId);
        if (convo is null)
        {
            // We can't load if the conversation is gone.
            Log.ConversationNotFound(conversationId,
                viewContext.FromMember.Id,
                viewContext.FromMember.OrganizationId);

            // Update our view to a message
            viewContext.SetResponseAction(new UpdateResponseAction(AlertModal.Render(
                $":warning: Conversation could not be found, contact '{WebConstants.SupportEmail}' for assistance.",
                "Internal Error")));

            return;
        }

        var definition = await GetFormDefinitionAsync(convo.Organization, formKey);
        var templateContext = await _templateContextFactory.CreateTicketTemplateContextAsync(convo, viewContext.FromMember);

        // Unpack the form results
        Dictionary<string, object?> formResults;
        try
        {
            formResults = _formEngine.ProcessFormSubmission(viewContext.Payload, definition, templateContext);
        }
        catch (InvalidFormFieldException iffex)
        {
            Log.FormDefinitionInvalid(iffex, formKey, iffex.FieldId, iffex.TemplateParameterName);
            viewContext.SetResponseAction(new UpdateResponseAction(AlertModal.Render("The form definition is invalid. Please contact support.", "Internal Error")));
            return;
        }

        foreach (var (key, value) in additionalProperties)
        {
            // If key already exists from form, keep that value
            formResults.TryAdd(key, value);
        }

        _ticketService.EnqueueTicketLinkRequest<TSettings>(integrationId, convo, viewContext.FromMember, formResults);

        // Update our view to a message.
        var message = ConversationTracker.IsSupportee(viewContext.FromMember, convo.Room)
            ? ":white_check_mark: Got it! I’m going to work on creating that ticket now."
            : ":white_check_mark: Got it! I’m going to work on creating that ticket now. I’ll send you a DM when I’m done.";
        viewContext.SetResponseAction(new UpdateResponseAction(AlertModal.Render(
            message,
            "Request Accepted")));
    }

    public bool TryTranslateForm(
        string formKey,
        FormDefinition formDefinition,
        CreateTicketTemplateContext context,
        out IList<ILayoutBlock> formBlocks)
    {
        try
        {
            formBlocks = _formEngine.TranslateForm(formDefinition, context);
            return true;
        }
        catch (InvalidFormFieldException iffex)
        {
            Log.FormDefinitionInvalid(iffex, formKey, iffex.FieldId, iffex.TemplateParameterName);
            formBlocks = Array.Empty<ILayoutBlock>();
            return false;
        }
    }
}

static partial class TicketModalServiceLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Could not find conversation '{ConversationId}' in response to link request from member '{MemberId}' in organization '{OrganizationId}'.")]
    public static partial void ConversationNotFound(this ILogger<TicketModalService> logger,
        int conversationId, int memberId, int organizationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Critical,
        Message =
            "The form definition for {FormKey} is invalid. Field {FieldId} has an invalid {TemplateParameterName}")]
    public static partial void FormDefinitionInvalid(this ILogger<TicketModalService> logger,
        Exception ex, string formKey, string? fieldId, string? templateParameterName);
}

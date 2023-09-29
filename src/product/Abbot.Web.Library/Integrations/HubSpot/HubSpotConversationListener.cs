using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Integrations.HubSpot;

/// <summary>
/// Listens to conversation events (new messages that are part of a <see cref="Conversation"/>) and posts them to
/// HubSpot.
/// </summary>
public class HubSpotConversationListener : IConversationListener
{
    static readonly ILogger<HubSpotConversationListener> Log =
        ApplicationLoggerFactory.CreateLogger<HubSpotConversationListener>();

    readonly IHubSpotClientFactory _clientFactory;
    readonly IIntegrationRepository _integrationRepository;
    readonly AbbotContext _dbContext;
    readonly IOptions<HubSpotOptions> _options;
    readonly HubSpotFormatter _formatter;
    readonly ConversationMessageToHtmlFormatter _htmlFormatter;

    public HubSpotConversationListener(
        IHubSpotClientFactory clientFactory,
        IIntegrationRepository integrationRepository,
        AbbotContext dbContext,
        IOptions<HubSpotOptions> options,
        HubSpotFormatter formatter,
        ConversationMessageToHtmlFormatter htmlFormatter)
    {
        _clientFactory = clientFactory;
        _integrationRepository = integrationRepository;
        _dbContext = dbContext;
        _options = options;
        _formatter = formatter;
        _htmlFormatter = htmlFormatter;
    }

    public async Task OnNewMessageAsync(Conversation conversation, ConversationMessage message)
    {
        if (!message.IsLive)
        {
            // Mostly to avoid syncing from-HubSpot messages back to HubSpot
            return;
        }

        // Is this conversation linked to a HubSpot ticket?
        var ticketLink = conversation.GetHubSpotLink();
        if (ticketLink is null)
        {
            // Nope. This is none of our bees-ness 🐝
            return;
        }

        using var scope = Log.BeginHubSpotTicketScope(ticketLink);
        Log.CommentObserved();

        var (client, _) = await CreateClientAsync(conversation.Organization);
        if (client is null)
        {
            // No credentials, or no settings.
            return;
        }

        if (ticketLink.ThreadId is { } threadId)
        {
            var htmlBody = await _htmlFormatter.FormatMessageAsHtmlAsync(message, conversation.Organization);

            // Post to the HubSpot Conversation thread.
            var comment = new HubSpotComment(
                Text: $"New reply from {message.From.DisplayName} in Slack\n\n{message.Text}",
                RichText: htmlBody);

            try
            {
                await client.CreateCommentAsync(threadId, comment);
                Log.CommentCreated(threadId);
            }
            catch (ApiException apiex)
            {
                Log.ErrorCreatingComment(apiex, threadId);
            }
        }

        // Do we have the timeline event registered?
        if (!_options.Value.TimelineEvents.TryGetValue(TimelineEvents.SlackMessagePosted, out var timelineEvent))
        {
            Log.NoTimelineEvent(TimelineEvents.SlackMessagePosted);
            return;
        }

        var evt = new TimelineEvent
        {
            ObjectId = ticketLink.TicketId,
            EventTemplateId = timelineEvent,
            Tokens = new Dictionary<string, object?>
            {
                {"slackAuthorAvatar", message.From.User.Avatar},
                {"slackAuthorName", message.From.DisplayName},
                {"slackAuthorUrl", message.From.FormatPlatformUrl()},
                {"slackMessage", await _formatter.FormatConversationMessageForTimelineEventAsync(message)},
                {
                    "slackMessageUrl",
                    message.GetMessageUrl()
                },
            }
        };
        try
        {
            await client.CreateTimelineEventAsync(evt);
            Log.TimelineEventCommentCreated();
        }
        catch (ApiException apiex)
        {
            Log.ErrorCreatingTimelineEventComment(apiex);
        }
    }

    async Task<(IHubSpotClient?, HubSpotSettings?)> CreateClientAsync(Organization organization)
    {
        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(organization);

        if (integration is not { Enabled: true }
            || settings is not { HasApiCredentials: true })
        {
            Log.NoActiveHubSpotIntegration();
            return (null, settings);
        }

        return (await _clientFactory.CreateClientAsync(integration, settings), settings);
    }

    public async Task OnStateChangedAsync(StateChangedEvent stateChange)
    {
        var conversation = stateChange.Conversation;
        var actor = stateChange.Member;

        // Abbot state changes (e.g. to Overdue) shouldn't change ticket status
        if (actor.IsAbbot())
        {
            return;
        }

        // Pretty sure this should always be loaded?
        if (!conversation.Links.IsLoaded)
        {
            Log.LinksNotLoaded();
            await _dbContext.Entry(conversation).Collection(c => c.Links).LoadAsync();
        }

        // Is this conversation linked to a HubSpot ticket?
        var ticketLink = conversation.GetHubSpotLink();
        if (ticketLink is null)
        {
            // Nope. This is none of our bees-ness 🐝
            return;
        }

        using var scope = Log.BeginHubSpotTicketScope(ticketLink);

        var (client, settings) = await CreateClientAsync(conversation.Organization);
        if (client is null || settings is null)
        {
            // No credentials, or no settings.
            return;
        }

        var ticket = await client.SafelyGetTicketAsync(ticketLink.TicketId);
        if (ticket is null)
        {
            return;
        }

        var isSupportee = ConversationTracker.IsSupportee(actor, conversation.Room);
        var newStage = NewTicketStatus(conversation, isSupportee, settings);

        if (newStage is null)
        {
            Log.TicketPipelineStageNotConfigured(conversation.State, isSupportee);
            return;
        }

        // Skip status update if different pipeline
        if (!ticket.Properties.TryGetValue("hs_pipeline", out var ticketPipeline)
            || ticketPipeline != settings.TicketPipelineId)
        {
            Log.TicketPipelineMismatch(settings.TicketPipelineId, ticketPipeline);
            return;
        }

        // Skip status update if already aligned
        if (ticket.Properties.TryGetValue("hs_pipeline_stage", out var ticketPipelineStage)
            && ticketPipelineStage == newStage)
        {
            Log.TicketPipelineStageMatch(ticketPipelineStage);
            return;
        }

        Log.TicketPipelineStageUpdate(ticketPipelineStage, newStage);

        await UpdateTicketAndLogErrorAsync(client,
            ticketLink.TicketId,
            new Dictionary<string, string?>()
            {
                ["hs_pipeline_stage"] = newStage,
            });
    }

    static async Task<HubSpotTicket?> UpdateTicketAndLogErrorAsync(
        IHubSpotClient client,
        string ticketId,
        IDictionary<string, string?> properties)
    {
        try
        {
            return await FaultHandler.RetryOnceAsync(
                () => client.UpdateTicketAsync(ticketId, new() { Properties = properties }));
        }
        catch (ApiException apiex)
        {
            Log.ErrorUpdatingTicket(apiex);
            return null;
        }
    }

    static string? NewTicketStatus(Conversation conversation, bool isSupportee, HubSpotSettings settings) =>
        conversation.State switch
        {
            ConversationState.Waiting => settings.WaitingTicketPipelineStageId,
            ConversationState.Snoozed => settings.NeedsResponseTicketPipelineStageId,
            ConversationState.Closed => settings.ClosedTicketPipelineStageId,
            ConversationState.New => settings.NewTicketPipelineStageId,
            _ when !isSupportee => settings.WaitingTicketPipelineStageId,
            _ => settings.NeedsResponseTicketPipelineStageId,
        };
}

static partial class HubSpotIntegrationLoggingExtensions
{
    static readonly Func<ILogger, Uri, IDisposable?> HubSpotTicketScope =
        LoggerMessage.DefineScope<Uri>("HubSpotTicketLink={HubSpotTicketLink}");

    public static IDisposable? BeginHubSpotTicketScope(this ILogger logger,
        HubSpotTicketLink hubSpotTicketLink) => HubSpotTicketScope(logger, hubSpotTicketLink.ApiUrl);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Cannot sync message to HubSpot, no active HubSpot integration with valid credentials")]
    public static partial void NoActiveHubSpotIntegration(this ILogger<HubSpotConversationListener> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Comment observed on conversation linked to HubSpot ticket")]
    public static partial void CommentObserved(this ILogger<HubSpotConversationListener> logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed to create a HubSpot Timeline Event comment")]
    public static partial void ErrorCreatingTimelineEventComment(this ILogger<HubSpotConversationListener> logger, Exception ex);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Timeline Event Created")]
    public static partial void TimelineEventCommentCreated(this ILogger<HubSpotConversationListener> logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "No timeline event registered for {TimelineEvent}")]
    public static partial void NoTimelineEvent(this ILogger<HubSpotConversationListener> logger, string timelineEvent);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Failed to create a HubSpot Conversations Comment {ThreadId}")]
    public static partial void ErrorCreatingComment(
        this ILogger<HubSpotConversationListener> logger,
        Exception ex,
        long threadId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Comment Created on thread {ThreadId}")]
    public static partial void CommentCreated(
        this ILogger<HubSpotConversationListener> logger,
        long threadId);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Failed to update HubSpot Ticket: {HubSpotErrorCode} - {HubSpotErrorDetail}. Validation Errors: {HubSpotValidationErrors}")]
    public static partial void ErrorUpdatingTicket(this ILogger<HubSpotConversationListener> logger,
        Exception ex,
        string? hubSpotErrorCode, string? hubSpotErrorDetail, string? hubSpotValidationErrors);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message =
            "Ticket Updated. Audit ID: {HubSpotAuditId}, Audit Type: {HubSpotAuditType}, Comment ID: {HubSpotCommentId}")]
    public static partial void TicketUpdated(
        this ILogger<HubSpotConversationListener> logger,
        long? hubSpotAuditId,
        string hubSpotAuditType,
        long? hubSpotCommentId);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Error,
        Message = "Failed to update HubSpot Ticket.")]
    public static partial void ErrorUpdatingTicket(
        this ILogger<HubSpotConversationListener> logger,
        Exception ex);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Warning,
        Message = "Conversation.Links not loaded.")]
    public static partial void LinksNotLoaded(
        this ILogger<HubSpotConversationListener> logger);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Warning,
        Message = "Pipeline mismatch. Expected={HubSpotPipelineExpected}; Actual={HubSpotPipelineActual}")]
    public static partial void TicketPipelineMismatch(
        this ILogger<HubSpotConversationListener> logger,
        string? hubSpotPipelineExpected,
        string? hubSpotPipelineActual);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "Skipping redundant stage update. Stage={HubSpotPipelineStage}")]
    public static partial void TicketPipelineStageMatch(
        this ILogger<HubSpotConversationListener> logger,
        string? hubSpotPipelineStage);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Information,
        Message = "Stage not configured. State={ConversationState} IsSupportee={IsSupportee}")]
    public static partial void TicketPipelineStageNotConfigured(
        this ILogger<HubSpotConversationListener> logger,
        ConversationState conversationState,
        bool isSupportee);

    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Information,
        Message = "Updating stage. Old={HubSpotPipelineStageOld} New={HubSpotPipelineStageNew}")]
    public static partial void TicketPipelineStageUpdate(
        this ILogger<HubSpotConversationListener> logger,
        string? hubSpotPipelineStageOld,
        string? hubSpotPipelineStageNew);
}

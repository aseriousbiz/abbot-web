using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Refit;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Integrations.Zendesk;

public class SlackToZendeskConversationListener : IConversationListener, ISlackToZendeskCommentImporter
{
    static readonly ILogger<SlackToZendeskConversationListener> Log =
        ApplicationLoggerFactory.CreateLogger<SlackToZendeskConversationListener>();

    readonly IZendeskClientFactory _clientFactory;
    readonly IZendeskResolver _zendeskResolver;
    readonly IIntegrationRepository _integrationRepository;
    readonly IUserRepository _userRepository;
    readonly AbbotContext _dbContext;
    readonly ISettingsManager _settingsManager;
    readonly TicketNotificationService _ticketNotificationService;
    readonly ZendeskFormatter _zendeskFormatter;

    public SlackToZendeskConversationListener(
        IZendeskClientFactory clientFactory,
        IZendeskResolver zendeskResolver,
        IIntegrationRepository integrationRepository,
        IUserRepository userRepository,
        AbbotContext dbContext,
        ISettingsManager settingsManager,
        TicketNotificationService ticketNotificationService,
        ZendeskFormatter zendeskFormatter)
    {
        _clientFactory = clientFactory;
        _zendeskResolver = zendeskResolver;
        _integrationRepository = integrationRepository;
        _userRepository = userRepository;
        _dbContext = dbContext;
        _settingsManager = settingsManager;
        _ticketNotificationService = ticketNotificationService;
        _zendeskFormatter = zendeskFormatter;
    }

    public async Task OnNewMessageAsync(Conversation conversation, ConversationMessage message)
    {
        if (!message.IsLive)
        {
            // Mostly to avoid syncing from-Zendesk messages back to Zendesk
            return;
        }

        // Is this conversation linked to a Zendesk ticket?
        var ticketLink = conversation.GetZendeskLink();
        if (ticketLink is null)
        {
            // Nope. This is none of our bees-ness 🐝
            return;
        }

        using var scope = Log.BeginZendeskTicketScope(ticketLink);
        Log.CommentObserved();

        var client = await CreateClientAsync(conversation.Organization);
        if (client is null)
        {
            // No credentials, or no settings.
            return;
        }

        if (await GetUnclosedTicketAsync(client, ticketLink) is not { } ticket)
        {
            return;
        }

        await ImportMessageAsync(conversation, client, message, ticket, ticketLink);
    }

    /// <summary>
    /// Called when creating a ticket and the existing conversation thread should be imported.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> on which the messages was received.</param>
    /// <param name="messages">The set of <see cref="ConversationMessage"/>s that make up the thread.</param>
    public async Task ImportThreadAsync(Conversation conversation, IEnumerable<ConversationMessage> messages)
    {
        // Is this conversation linked to a Zendesk ticket?
        var ticketLink = conversation.GetZendeskLink();
        if (ticketLink is null)
        {
            // Nope. This is none of our bees-ness 🐝
            return;
        }

        using var scope = Log.BeginZendeskTicketScope(ticketLink);
        // TODO: we're missing some Conversation scopes
        Log.BeginImportThread(conversation.FirstMessageId);

        var client = await CreateClientAsync(conversation.Organization);
        if (client is null)
        {
            // No credentials, or no settings.
            return;
        }

        if (await GetUnclosedTicketAsync(client, ticketLink) is not { } ticket)
        {
            return;
        }

        foreach (var message in messages)
        {
            await ImportMessageAsync(conversation, client, message, ticket, ticketLink);
        }
    }

    async Task<IZendeskClient?> CreateClientAsync(Organization organization)
    {
        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);

        if (integration is not { Enabled: true }
            || settings is not { HasApiCredentials: true })
        {
            Log.NoActiveZendeskIntegration();
            return null;
        }

        return _clientFactory.CreateClient(settings);
    }

    async Task ImportMessageAsync(
        Conversation conversation,
        IZendeskClient client,
        ConversationMessage message,
        ZendeskTicket zendeskTicket,
        ZendeskTicketLink ticketLink)
    {
        using var scope = Log.BeginMessageScope(message.MessageId, message.ThreadId);
        // Resolve the user
        var user = await _zendeskResolver.ResolveZendeskIdentityAsync(
            client,
            conversation.Organization,
            message.From,
            null);

        if (user is null)
        {
            Log.FailedToResolveZendeskUser();
            return;
        }
        using var _ = Log.BeginZendeskUserScope(user);

        // Post the comment!
        var comment = await _zendeskFormatter.CreateCommentAsync(conversation, message, user.Id);

        // Need to preserve all existing properties
        zendeskTicket.Comment = comment;

        var updatedTicket = await UpdateTicketAndLogErrorAsync(client,
            ticketLink.TicketId,
            new()
            {
                Body = zendeskTicket,
            },
            conversation,
            $"add new comment to <{ticketLink.WebUrl}|Zendesk ticket>",
            message.From);
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

        // Is this conversation linked to a Zendesk ticket?
        var ticketLink = conversation.GetZendeskLink();
        if (ticketLink is null)
        {
            // Nope. This is none of our bees-ness 🐝
            return;
        }

        using var scope = Log.BeginZendeskTicketScope(ticketLink);

        var client = await CreateClientAsync(conversation.Organization);
        if (client is null)
        {
            // No credentials, or no settings.
            return;
        }

        if (await GetUnclosedTicketAsync(client, ticketLink) is not { } body)
        {
            return;
        }

        var isSupportee = ConversationTracker.IsSupportee(actor, conversation.Room);
        var newStatus = NewTicketStatus(conversation, isSupportee, body.Status);

        // Skip status update if already aligned
        if (body.Status is not { } currentStatus
            || string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Log.TicketStatusUpdating(currentStatus, newStatus);

        body.Status = newStatus;

        var updatedTicket = await UpdateTicketAndLogErrorAsync(client,
            ticketLink.TicketId,
            new()
            {
                Body = body,
            },
            conversation,
            $"set <{ticketLink.WebUrl}|Zendesk ticket> status to *{newStatus}*",
            actor);

        if (updatedTicket?.Body?.Status is { } updatedStatus)
        {
            var abbot = await _userRepository.EnsureAbbotMemberAsync(conversation.Organization);
            await _settingsManager.SetTicketStatusAsync(conversation, updatedStatus, abbot);
        }
    }

    static async Task<ZendeskTicket?> GetUnclosedTicketAsync(IZendeskClient client, ZendeskTicketLink ticketLink)
    {
        var body = await client.SafelyGetTicketAsync(ticketLink.TicketId);
        if (body is null)
        {
            Log.TicketNotFound();
            return null;
        }

        if (body.Status is "closed")
        {
            Log.TicketClosed();
            return null;
        }

        return body;
    }

    async Task<TicketMessage?> UpdateTicketAndLogErrorAsync(
        IZendeskClient client,
        long ticketId,
        TicketMessage updated,
        Conversation conversation,
        string action,
        Member actor)
    {
        try
        {
            var response = await FaultHandler.RetryOnceAsync(() => client.UpdateTicketAsync(ticketId, updated));

            foreach (var evt in response.Audit?.Events ?? Array.Empty<TicketAuditEvent>())
            {
                if (evt.Type is "WebhookEvent")
                {
                    continue;
                }

                var commentId = evt.Type == "Comment" && evt.AdditionalProperties.TryGetValue("audit_id", out var id)
                    ? (long?)id
                    : null;

                Log.TicketUpdated(
                    evt.Id,
                    evt.Type,
                    evt.FieldName,
                    commentId);
            }

            return response;
        }
        catch (ApiException apiex)
        {
            if (apiex.TryGetErrorDetail(out var code, out var description, out var details))
            {
                var validationErrors = Array.Empty<string>();
                if (code == "RecordInvalid" && details?.Property("base", StringComparison.InvariantCulture) is { Value: JArray errors })
                {
                    // Try to extract validation errors
                    validationErrors = errors
                        .Select(e => e is JObject eObj
                            ? eObj.Value<string>("description")
                            : null)
                        .WhereNotNull()
                        .ToArray();
                }
                Log.ErrorUpdatingTicket(apiex, code, description, string.Join("; ", validationErrors));

                await _ticketNotificationService.PublishAsync(
                    conversation,
                    NotificationType.TicketError,
                    "Ticket Error",
                    validationErrors.Length switch
                    {
                        0 => $"Could not {action} due to a validation error.",
                        _ => $"""
                              Could not {action}:
                              • {string.Join("\n• ", validationErrors)}
                              """
                    },
                    actor);
            }
            else
            {
                Log.ErrorUpdatingTicket(apiex);
            }

            return null;
        }
    }

    static string? NewTicketStatus(Conversation conversation, bool isSupportee, string? currentStatus) =>
        conversation.State switch
        {
            ConversationState.Waiting => "pending",
            ConversationState.Snoozed => "open",
            ConversationState.Closed => "solved",
            ConversationState.New when currentStatus is "new" => "new",
            _ when !isSupportee => "pending",
            _ => "open",
        };
}

static partial class ZendeskIntegrationLoggingExtensions
{
    static readonly Func<ILogger, Uri?, IDisposable?> ZendeskOrganizationScope =
        LoggerMessage.DefineScope<Uri?>("ZendeskOrganizationLink={ZendeskOrganizationLink}");

    public static IDisposable? BeginZendeskOrganizationScope(this ILogger logger, ZendeskOrganizationLink? zendeskOrganizationLink)
        => ZendeskOrganizationScope(logger, zendeskOrganizationLink?.ApiUrl);

    static readonly Func<ILogger, string, long?, IDisposable?> ZendeskUserScope =
        LoggerMessage.DefineScope<string, long?>("ZendeskUserLink={ZendeskUserLink} ZendeskUserOrganizationId={ZendeskUserOrganizationId}");

    public static IDisposable? BeginZendeskUserScope(this ILogger logger, ZendeskUserLink zendeskUserLink)
        => ZendeskUserScope(logger, zendeskUserLink.ApiUrl.ToString(), null);

    public static IDisposable? BeginZendeskUserScope(this ILogger logger, ZendeskUser zendeskUser) =>
        ZendeskUserScope(logger, zendeskUser.Url, zendeskUser.OrganizationId);

    static readonly Func<ILogger, Uri, IDisposable?> ZendeskTicketScope =
        LoggerMessage.DefineScope<Uri>("ZendeskTicketLink={ZendeskTicketLink}");

    public static IDisposable? BeginZendeskTicketScope(this ILogger logger,
        ZendeskTicketLink zendeskTicketLink) => ZendeskTicketScope(logger, zendeskTicketLink.ApiUrl);

    static readonly Func<ILogger, string, IDisposable?> ImportThreadScope =
        LoggerMessage.DefineScope<string>(
            "Import Thread, ThreadId={ThreadId}");

    public static IDisposable? BeginImportThread(
        this ILogger<SlackToZendeskConversationListener> logger,
        string threadId) => ImportThreadScope(logger, threadId);

    static readonly Func<ILogger, string, string?, IDisposable?> MessageScope =
        LoggerMessage.DefineScope<string, string?>("Message Scope: {MessageId}, Thread ID: {ThreadId}");

    public static IDisposable? BeginMessageScope(
        this ILogger<SlackToZendeskConversationListener> logger,
        string messageId, string? threadId) => MessageScope(logger, messageId, threadId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Cannot sync message to Zendesk, no active Zendesk integration with valid credentials")]
    public static partial void NoActiveZendeskIntegration(this ILogger<SlackToZendeskConversationListener> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Comment observed on conversation linked to Zendesk ticket")]
    public static partial void CommentObserved(this ILogger<SlackToZendeskConversationListener> logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Failed to resolve Zendesk identity")]
    public static partial void FailedToResolveZendeskUser(this ILogger<SlackToZendeskConversationListener> logger);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Failed to update Zendesk Ticket: {ZendeskErrorCode} - {ZendeskErrorDetail}. Validation Errors: {ZendeskValidationErrors}")]
    public static partial void ErrorUpdatingTicket(this ILogger<SlackToZendeskConversationListener> logger,
        Exception ex,
        string? zendeskErrorCode, string? zendeskErrorDetail, string? zendeskValidationErrors);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message =
            "Ticket Updated. Audit ID: {ZendeskAuditId}, Audit Type: {ZendeskAuditType}, Field: {ZendeskFieldName}, Comment ID: {ZendeskCommentId}")]
    public static partial void TicketUpdated(
        this ILogger<SlackToZendeskConversationListener> logger,
        long? zendeskAuditId,
        string zendeskAuditType,
        string? zendeskFieldName,
        long? zendeskCommentId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Failed to update Zendesk Ticket.")]
    public static partial void ErrorUpdatingTicket(
        this ILogger<SlackToZendeskConversationListener> logger,
        Exception ex);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Conversation.Links not loaded.")]
    public static partial void LinksNotLoaded(
        this ILogger<SlackToZendeskConversationListener> logger);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Ticket not found.")]
    public static partial void TicketNotFound(
        this ILogger<SlackToZendeskConversationListener> logger);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Ticket closed; skipping update.")]
    public static partial void TicketClosed(
        this ILogger<SlackToZendeskConversationListener> logger);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Ticket status updating. Old={ZendeskStatusOld} New={ZendeskStatusNew}")]
    public static partial void TicketStatusUpdating(
        this ILogger<SlackToZendeskConversationListener> logger,
        string? zendeskStatusOld,
        string? zendeskStatusNew);
}

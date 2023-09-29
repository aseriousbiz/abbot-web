using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Integrations.HubSpot;

/// <summary>
/// Creates Slack messages from new HubSpot Conversation messages for HubSpot Conversations associated with a
/// linked ticket.
/// </summary>
public class HubSpotToSlackImporter
{
    static readonly ILogger<HubSpotToSlackImporter> Log = ApplicationLoggerFactory.CreateLogger<HubSpotToSlackImporter>();

    readonly IIntegrationRepository _integrationRepository;
    readonly IHubSpotClientFactory _hubSpotClientFactory;
    readonly ITextAnalyticsClient _textAnalyticsClient;
    readonly HubSpotOptions _hubSpotOptions;
    readonly ISlackApiClient _slackApiClient;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly ISettingsManager _settingsManager;

    public HubSpotToSlackImporter(
        IIntegrationRepository integrationRepository,
        IHubSpotClientFactory hubSpotClientFactory,
        ITextAnalyticsClient textAnalyticsClient,
        IOptions<HubSpotOptions> hubSpotOptions,
        ISlackApiClient slackApiClient,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        ISettingsManager settingsManager)
    {
        _integrationRepository = integrationRepository;
        _hubSpotClientFactory = hubSpotClientFactory;
        _textAnalyticsClient = textAnalyticsClient;
        _hubSpotOptions = hubSpotOptions.Value;
        _slackApiClient = slackApiClient;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _settingsManager = settingsManager;
    }

    public static string GetImportKey(long threadId, string messageId) =>
        $"HubSpotMessageImport:{threadId}:{messageId}";

    [AutomaticRetry(Attempts = 0)]
    [Queue(HangfireQueueNames.HighPriority)]
    public async Task<SlackMessage?> ImportMessageAsync(int settingId, string messageId, long threadId, long portalId)
    {
        using (Log.BeginImportingMessage(messageId, threadId, portalId))
        {
            var setting = await _settingsManager.GetAsync(
                SettingsScope.HubSpotPortal(portalId),
                name: GetImportKey(threadId, messageId));

            // Extra assurance we're only importing this message once.
            if (setting?.Id == settingId)
            {
                return await ImportMessageInScopeAsync(messageId, threadId, portalId);
            }
        }

        return null;
    }

    async Task<SlackMessage?> ImportMessageInScopeAsync(string messageId, long threadId, long portalId)
    {
        if (await CreateHubSpotClientAsync(portalId) is not { } hubSpotClient
            || await FindLinkedConversation(threadId, portalId, hubSpotClient) is not { } conversationLink)
        {
            return null;
        }

        using var orgScope = Log.BeginOrganizationScope(conversationLink.Organization);
        using var convoScope = Log.BeginConversationRoomAndHubScopes(conversationLink.Conversation);

        return await CreateSlackReplyFromHubSpotMessageAsync(messageId, threadId, conversationLink, hubSpotClient);
    }

    async Task<SlackMessage?> CreateSlackReplyFromHubSpotMessageAsync(
        string messageId,
        long threadId,
        ConversationLink conversationLink,
        IHubSpotClient hubSpotClient)
    {
        var conversation = conversationLink.Conversation;
        var ticketUrl = conversationLink.ExternalId;
        using (Log.BeginCreatingSlackMessage(ticketUrl, conversation.Id, conversation.Organization.PlatformId))
        {
            try
            {
                // Ok, we're ready to import the message to the conversation.
                var message = await hubSpotClient.GetMessageAsync(threadId, messageId);
                // Messages posted by Abbot will have Client.IntegrationAppId set to our HubSpot App Id.
                // So we can filter out our own messages this way.
                if (message is { Direction: "OUTGOING" }
                    && !(message.Client.ClientType is "INTEGRATION" && message.Client.IntegrationAppId == _hubSpotOptions.AppId))
                {
                    return await CreateSlackMessageAsync(message, conversation, ticketUrl);
                }

                Log.SkippingMessage(message?.Direction ?? "(null)", message?.Client.ClientType ?? "(null)");
            }
            catch (ApiException e)
            {
                // This is a bit of a hack. We're not sure why this happens, but it does.
                // We'll just ignore it for now.
                Log.ErrorRetrievingMessage(e);
            }
        }

        return null;
    }

    async Task<IHubSpotClient?> CreateHubSpotClientAsync(long portalId)
    {
        var (integration, settings) = await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(
            externalId: $"{portalId}");

        if (settings is null || integration is null)
        {
            Log.MissingSettings();
            return null;
        }

        if (!settings.HasApiCredentials)
        {
            Log.MissingApiCredentials();
            return null;
        }

        return await _hubSpotClientFactory.CreateClientAsync(integration, settings);
    }

    async Task<ConversationLink?> FindLinkedConversation(long threadId, long portalId, IHubSpotClient hubSpotClient)
    {
        // It seems in practice, this only returns one, but we want to be defensive here.
        var potentialTicketIds = await hubSpotClient.GetTicketsAssociatedWithHubSpotConversation(threadId);

        // Let's find the first one that matches a linked ticket.
        foreach (var potentialTicketId in potentialTicketIds)
        {
            var potentialTicketUrl = HubSpotLinker.GetTicketUrl(portalId, potentialTicketId);
            var conversationLink = await _conversationRepository.GetConversationLinkAsync(
                ConversationLinkType.HubSpotTicket,
                externalId: potentialTicketUrl.ToString());

            if (conversationLink is not null)
            {
                return conversationLink;
            }

            Log.NoConversationForTicket(potentialTicketUrl.ToString());
        }

        return null;
    }

    async Task<SlackMessage?> CreateSlackMessageAsync(
        HubSpotMessage message,
        Conversation conversation,
        string ticketUrl)
    {
#pragma warning disable CA1826
        var sender = message.Senders.FirstOrDefault();
#pragma warning restore CA1826
        var apiToken = conversation.Organization.RequireAndRevealApiToken();
        var abbot = await _userRepository.EnsureAbbotMemberAsync(conversation.Organization);
        var slackAuthor = new SlackMessageAuthor($"{sender?.Name ?? "No name provided"}", null, null);

        // TODO: Parse HTML messages and convert to blocks.

        var blocks = new List<ILayoutBlock>
        {
            new Section(new MrkdwnText(message.Text)),
            new Context(new MrkdwnText($"This comment was posted on the <{ticketUrl}|linked HubSpot ticket>.")),
        };

        var messageRequest = new MessageRequest(Channel: conversation.Room.PlatformRoomId, Text: message.Text)
        {
            ThreadTs = conversation.FirstMessageId,
            UserName = slackAuthor.Member is null ? $"{slackAuthor.DisplayName} (from HubSpot)" : slackAuthor.DisplayName,
            IconUrl = slackAuthor.AvatarUrl is null ? null : new(slackAuthor.AvatarUrl),
            Blocks = blocks
        };

        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);

        if (!response.Ok)
        {
            Log.ErrorPostingSlackComment(slackError: response.ToString());
            return null;
        }

        Log.SyncedComment(syncedMessageId: response.Body.Timestamp ?? "(null)");
        var messageId = SlackTimestamp.Parse(response.Body.Timestamp.Require());

        var sensitiveValues = await _textAnalyticsClient.RecognizePiiEntitiesAsync(message.Text);

        var messagePostedEvent = new MessagePostedEvent
        {
            MessageId = response.Body.Timestamp,
            MessageUrl = SlackFormatter.MessageUrl(conversation.Organization.Domain,
                conversation.Room.PlatformRoomId,
                response.Body.Timestamp,
                response.Body.ThreadTimestamp),
            ExternalSource = "HubSpot",
            ExternalMessageId = ticketUrl,
            ExternalAuthorId = null, //TODO: This,
            ExternalAuthor = slackAuthor.DisplayName,
            Metadata = new MessagePostedMetadata
            {
                Categories = Array.Empty<Category>(),
                Text = message.Text,
                SensitiveValues = sensitiveValues,

            }.ToJson(),
        };

        var isSupportee = slackAuthor.Member is null
            || ConversationTracker.IsSupportee(slackAuthor.Member, conversation.Room);
        await _conversationRepository.UpdateForNewMessageAsync(
            conversation,
            messagePostedEvent,
            new ConversationMessage(
                messageRequest.Text ?? "",
                conversation.Organization,
                slackAuthor.Member ?? abbot,
                conversation.Room,
                messageId.UtcDateTime,
                response.Body.Timestamp,
                conversation.FirstMessageId,
                blocks,
                Array.Empty<FileUpload>(),
                MessageContext: null),
            isSupportee);

        return response.Body;
    }
}

public static partial class HubSpotMessageToSlackImporterLoggingExtensions
{
    static readonly Func<ILogger, string, long, long, IDisposable?> ImportingHubSpotMessageScope =
        LoggerMessage.DefineScope<string, long, long>("Syncing HubSpot Message. HubSpotMessageId={HubSpotMessageId}, HubSpotThreadId={HubSpotThreadId}, PortalId={PortalId}");

    public static IDisposable? BeginImportingMessage(
        this ILogger<HubSpotToSlackImporter> logger,
        string hubSpotMessageId,
        long hubSpotThreadId,
        long portalId) =>
        ImportingHubSpotMessageScope(logger, hubSpotMessageId, hubSpotThreadId, portalId);

    static readonly Func<ILogger, string, int, string, IDisposable?> CreatingSlackMessageScope =
        LoggerMessage.DefineScope<string, int, string>("Syncing HubSpot Message. TicketUrl={TicketUrl}, ConversationId={ConversationId}, PlatformId={PlatformId}");

    public static IDisposable? BeginCreatingSlackMessage(this ILogger<HubSpotToSlackImporter> logger,
        string ticketUrl,
        int conversationId,
        string platformId) =>
        CreatingSlackMessageScope(logger, ticketUrl, conversationId, platformId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Error trying to retrieve message.")]
    public static partial void ErrorRetrievingMessage(this ILogger<HubSpotToSlackImporter> logger, Exception ex);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Skipping message because the direction {Direction} is not OUTGOING or client {ClientType} is not HUBSPOT.")]
    public static partial void SkippingMessage(
        this ILogger<HubSpotToSlackImporter> logger,
        string direction,
        string clientType);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "No conversation associated with ticket {HubSpotTicketUrl}")]
    public static partial void NoConversationForTicket(
        this ILogger<HubSpotToSlackImporter> logger,
        string hubSpotTicketUrl);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Error posting Slack comment: {SlackError}")]
    public static partial void ErrorPostingSlackComment(
        this ILogger<HubSpotToSlackImporter> logger,
        string? slackError);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Exception syncing comment, skipping it.")]
    public static partial void ExceptionSyncingComment(
        this ILogger<HubSpotToSlackImporter> logger, Exception ex);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Synced comment to Slack as message {SyncedMessageId}.")]
    public static partial void SyncedComment(this ILogger<HubSpotToSlackImporter> logger, string syncedMessageId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "Missing HubSpot settings")]
    public static partial void MissingSettings(this ILogger<HubSpotToSlackImporter> logger);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Error,
        Message = "Missing API Credentials")]
    public static partial void MissingApiCredentials(this ILogger<HubSpotToSlackImporter> logger);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "Not Importing because {FeatureFlag} feature flag not enabeld")]
    public static partial void FeatureFlagNotEnabled(this ILogger<HubSpotToSlackImporter> logger, string featureFlag);
}

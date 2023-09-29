using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Integrations.HubSpot;

public interface IHubSpotLinker : ITicketLinker<HubSpotSettings, HubSpotTicket>
{
    /// <summary>
    /// Creates a <see cref="ConversationLink" /> between a HubSpot ticket and a <see cref="Conversation"/>. Also
    /// creates a timeline event in HubSpot.
    /// </summary>
    /// <param name="ticketId">The Id of the ticket.</param>
    /// <param name="portalId">The HubSpot portal Id. Also known as a HubId.</param>
    /// <param name="threadId">The HubSpot Conversation thread associated with the ticket, if any.</param>
    /// <param name="conversation">The conversation to link.</param>
    /// <param name="client">The client to use</param>
    /// <param name="actor">The <see cref="Member"/> creating the link.</param>
    /// <returns>The <see cref="ConversationLink"/> for the external ticket.</returns>
    Task<ConversationLink> CreateConversationTicketLinkAsync(
        long ticketId,
        long portalId,
        long? threadId,
        Conversation conversation,
        IHubSpotClient client,
        Member actor);

    /// <summary>
    /// Attempts to find the HubSpot ticket associated with a <see cref="Conversation"/> and link the two. This will
    /// also try to link an associated HubSpot Conversation, if any.
    /// </summary>
    /// <param name="conversation">The conversation to link.</param>
    /// <param name="actor">The <see cref="Member"/> that is linking these conversations.</param>
    /// <returns>The <see cref="ConversationLink"/> for the external ticket.</returns>
    Task<ConversationLink?> LinkPendingConversationTicketAsync(Conversation conversation, Member actor);

    /// <summary>
    /// Creates a token we embed in a HubSpot form submission so we can find the ticket created as a result of
    /// that form submission and link it back to the conversation.
    /// </summary>
    /// <param name="conversation">The conversation that was linked.</param>
    /// <returns></returns>
    string CreateSearchToken(Conversation conversation);
}

public static class HubSpotLinkerExtensions
{
    /// <summary>
    /// Creates a <see cref="ConversationLink" /> between a HubSpot ticket and a <see cref="Conversation"/>. Also
    /// creates a timeline event in HubSpot.
    /// </summary>
    /// <param name="linker">The <see cref="IHubSpotLinker"/>.</param>
    /// <param name="ticketId">The Id of the ticket.</param>
    /// <param name="portalId">The HubSpot portal Id. Also known as a HubId.</param>
    /// <param name="conversation">The conversation to link.</param>
    /// <param name="client">The client to use</param>
    /// <param name="actor">The <see cref="Member"/> creating the link.</param>
    /// <returns>The <see cref="ConversationLink"/> for the external ticket.</returns>
    public static async Task<ConversationLink> CreateConversationTicketLinkAsync(
        this IHubSpotLinker linker,
        long ticketId,
        long portalId,
        Conversation conversation,
        IHubSpotClient client,
        Member actor) => await linker.CreateConversationTicketLinkAsync(
            ticketId,
            portalId,
            threadId: null,
            conversation,
            client,
            actor);
}

public partial class HubSpotLinker : IHubSpotLinker
{
    static readonly ILogger<HubSpotLinker> Log = ApplicationLoggerFactory.CreateLogger<HubSpotLinker>();

    readonly IIntegrationRepository _integrationRepository;
    readonly IHubSpotClientFactory _hubSpotClientFactory;
    readonly IHubSpotResolver _hubSpotResolver;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly ISettingsManager _settingsManager;
    readonly IUrlGenerator _urlGenerator;
    readonly HubSpotOptions _hubSpotOptions;
    readonly IClock _clock;

    public HubSpotLinker(
        IOptions<HubSpotOptions> hubSpotOptions,
        IIntegrationRepository integrationRepository,
        IHubSpotClientFactory hubSpotClientFactory,
        IHubSpotResolver hubSpotResolver,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        ISettingsManager settingsManager,
        IUrlGenerator urlGenerator,
        IBackgroundJobClient backgroundJobClient,
        IClock clock)
    {
        _integrationRepository = integrationRepository;
        _hubSpotClientFactory = hubSpotClientFactory;
        _hubSpotResolver = hubSpotResolver;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _backgroundJobClient = backgroundJobClient;
        _settingsManager = settingsManager;
        _urlGenerator = urlGenerator;
        _clock = clock;
        _hubSpotOptions = hubSpotOptions.Value;
    }

    public async Task<ConversationLink?> CreateConversationLinkAsync(
        Integration integration,
        HubSpotSettings settings,
        HubSpotTicket ticket,
        Conversation conversation,
        Member actor)
    {
        var ticketId = long.Parse(ticket.Id, CultureInfo.InvariantCulture);
        var portalId = long.Parse(integration.ExternalId.Require(), CultureInfo.InvariantCulture);

        var client = await _hubSpotClientFactory.CreateClientAsync(integration, settings);
        return await CreateConversationTicketLinkAsync(
            ticketId, portalId, threadId: null, conversation, client, actor);
    }

    public async Task<ConversationLink> CreateConversationTicketLinkAsync(
        long ticketId,
        long portalId,
        long? threadId,
        Conversation conversation,
        IHubSpotClient client,
        Member actor)
    {
        if (_hubSpotOptions.TimelineEvents.TryGetValue(TimelineEvents.LinkedSlackConversation, out var eventId))
        {
            var timelineEvent = await client.CreateTimelineEventAsync(new()
            {
                EventTemplateId = eventId.ToStringInvariant(),
                ObjectId = ticketId.ToStringInvariant(),
                Tokens = new Dictionary<string, object?>
                {
                    {"slackChannelID", conversation.Room.PlatformRoomId},
                    {"slackChannelName", conversation.Room.Name},
                    {"slackThreadUrl", conversation.GetFirstMessageUrl().ToString()}
                }
            });

            Log.TimelineEventCreated(
                TimelineEvents.LinkedSlackConversation,
                conversation.Id,
                timelineEvent.Id);
        }
        else
        {
            Log.TimelineEventMappingMissing(TimelineEvents.LinkedSlackConversation);
        }

        var ticketUrl = GetTicketUrl(portalId, ticketId);

        // Link the conversation to that ticket
        return await _conversationRepository.CreateLinkAsync(
            conversation,
            ConversationLinkType.HubSpotTicket,
            ticketUrl.ToString(),
            threadId != null ? new HubSpotLinkSettings(threadId.Value) : null,
            actor,
            _clock.UtcNow);
    }

    [Queue(HangfireQueueNames.NormalPriority)]
    [Obsolete("Use Id<> overload")]
    public Task<ConversationLink?> LinkPendingConversationTicketAsync(
        int conversationId,
        int actorId,
        int actorOrganizationId,
        int attemptedCount) =>
        LinkPendingConversationTicketAsync(
            new(conversationId),
            new(actorId),
            new(actorOrganizationId),
            attemptedCount);

    /// <summary>
    /// This overload is used to schedule the linking as a background task.
    /// </summary>
    /// <param name="conversationId">The database Id of the <see cref="Conversation"/> to link.</param>
    /// <param name="actorId">The database Id of the <see cref="Member"/> who initiated the linking.</param>
    /// <param name="actorOrganizationId"></param>
    /// <param name="attemptedCount">The number of attempts already attempted.</param>
    /// <returns>The <see cref="Uri"/> to the external ticket or null if not found.</returns>
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task<ConversationLink?> LinkPendingConversationTicketAsync(
        Id<Conversation> conversationId,
        Id<Member> actorId,
        Id<Organization> actorOrganizationId,
        int attemptedCount)
    {
        if (attemptedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptedCount));
        }

        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation is null)
        {
            return null;
        }

        var actor = await _userRepository.GetMemberByIdAsync(actorId, actorOrganizationId).Require();
        var link = await LinkPendingConversationTicketAsync(conversation, actor);

        if (link is null && attemptedCount < 5)
        {
            // Schedule this again.
            _backgroundJobClient.Schedule<HubSpotLinker>(
                linker => linker.LinkPendingConversationTicketAsync(
                    conversationId,
                    actorId,
                    actorOrganizationId,
                    attemptedCount + 1),
                TimeSpan.FromSeconds(10 * attemptedCount + 1));
        }

        return link;
    }

    public async Task<ConversationLink?> LinkPendingConversationTicketAsync(Conversation conversation, Member actor)
    {
        // We only support HubSpot for pending tickets at the moment.
        var (integration, settings) = await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(
            conversation.Organization);

        if (integration?.ExternalId is null || settings is null)
        {
            return null;
        }

        var client = await _hubSpotClientFactory.CreateClientAsync(integration, settings);
        var portalId = long.Parse(integration.ExternalId, CultureInfo.InvariantCulture);

        // Get existing link, if any.
        var conversationLink = conversation.Links.SingleOrDefault(l => l.LinkType is ConversationLinkType.HubSpotTicket);
        if (conversationLink is not null)
        {
            return conversationLink;
        }

        var tokenFormField = (await _settingsManager
                .GetHubSpotFormSettingsAsync(conversation.Organization)
                .Require())
            .TokenFormField;

        // Form fields can have prefixes like "TICKET.". We need to strip the prefix in order to do a search.
        var property = tokenFormField.RightAfter(".", StringComparison.Ordinal);
        var searchToken = CreateSearchToken(conversation);
        var searchResults = await client.SearchTicketsAsync(property, searchToken);
        if (searchResults is { Results: [var searchResult] })
        {
            var ticketId = long.Parse(searchResult.Id, CultureInfo.InvariantCulture);
            var ticketWebUrl = GetTicketUrl(portalId, ticketId);

            // Try and find the associated HubSpot Conversation thread, if any.
            var threadIds = await client.GetConversationsAssociatedWithHubSpotTicket(ticketId);
            if (threadIds is { Count: > 1 })
            {
                var foundThreads = string.Join(", ", threadIds.Select(c => c.ToString(CultureInfo.InvariantCulture)));
                Log.FoundMoreThanOneConversationForTicket(ticketId, foundThreads);
            }

            // TODO: Remove the search token from the description.
            return await CreateConversationTicketLinkAsync(
                ticketId,
                portalId,
#pragma warning disable CA1826
                threadIds.FirstOrDefault(),
#pragma warning restore CA1826
                conversation,
                client,
                actor);
        }

        return null;
    }

    const string TokenSeed = "CzYRsSNdQcGZpHXxwrpB";

    public string CreateSearchToken(Conversation conversation)
    {
        var hashedInfo = $"{conversation.Id}|{IntegrationType.HubSpot}".ComputeHMACSHA256FileName(secret: TokenSeed);
        return $"cnv_{conversation.Id}_{hashedInfo}";
    }

    public static Uri GetTicketUrl(long portalId, long ticketId) =>
        new HubSpotTicketLink(portalId, $"{ticketId}").WebUrl;
}

static partial class HubSpotLinkerLoggingExtensions
{
    static readonly Func<ILogger, string?, IDisposable?> HubSpotCompanyScope =
        LoggerMessage.DefineScope<string?>("HubSpotCompanyLink={HubSpotCompanyLink}");

    public static IDisposable? BeginHubSpotCompanyScope(this ILogger logger, HubSpotCompanyLink? integrationLink)
        => HubSpotCompanyScope(logger, integrationLink?.ToString());

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message =
            "Created a HubSpot timeline event {TimelineEventName} for Conversation {ConversationId}: {TimelineEventId}")]
    public static partial void TimelineEventCreated(this ILogger<HubSpotLinker> logger, string timelineEventName,
        int conversationId, string timelineEventId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message =
            "Not creating timeline event. Unable to find mapping for timeline event {TimelineEventName} in configuration")]
    public static partial void TimelineEventMappingMissing(this ILogger<HubSpotLinker> logger, string timelineEventName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Found more than one threads for ticket {TicketId}: {ThreadIds}")]
    public static partial void FoundMoreThanOneConversationForTicket(
        this ILogger<HubSpotLinker> logger,
        long ticketId,
        string threadIds);
}

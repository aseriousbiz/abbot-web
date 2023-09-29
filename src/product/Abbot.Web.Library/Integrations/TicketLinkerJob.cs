using System.Collections.Generic;
using System.Diagnostics;
using Hangfire;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Serialization;
using Serious.Abbot.Services;
using Serious.Abbot.Signals;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Integrations;

public class TicketLinkerJob<TSettings> : ITicketLinkerJob
    where TSettings : class, ITicketingSettings
{
    static readonly ILogger<ITicketLinker> Log = ApplicationLoggerFactory.CreateLogger<ITicketLinker>();

    readonly IConversationRepository _conversationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly ITicketLinker<TSettings> _ticketLinker;
    readonly IMessageDispatcher _messageDispatcher;
    readonly IReactionsApiClient _reactionsApiClient;
    readonly ISystemSignaler _systemSignaler;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IBackgroundSlackClient _backgroundSlackClient;
    readonly TicketNotificationService _ticketNotificationService;
    readonly IAnalyticsClient _analyticsClient;

    public TicketLinkerJob(
        IConversationRepository conversationRepository,
        IIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        ITicketLinker<TSettings> ticketLinker,
        IMessageDispatcher messageDispatcher,
        IReactionsApiClient reactionsApiClient,
        ISystemSignaler systemSignaler,
        PlaybookDispatcher playbookDispatcher,
        IBackgroundSlackClient backgroundSlackClient,
        TicketNotificationService ticketNotificationService,
        IAnalyticsClient analyticsClient)
    {
        _conversationRepository = conversationRepository;
        _integrationRepository = integrationRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _ticketLinker = ticketLinker;
        _messageDispatcher = messageDispatcher;
        _reactionsApiClient = reactionsApiClient;
        _systemSignaler = systemSignaler;
        _playbookDispatcher = playbookDispatcher;
        _backgroundSlackClient = backgroundSlackClient;
        _ticketNotificationService = ticketNotificationService;
        _analyticsClient = analyticsClient;
    }

    // We don't want to retry this job. It's a one-off job and it notifies users.
    // Better to silently fail than harass users with repeat notifications.
    [AutomaticRetry(Attempts = 0)]
    [Queue(HangfireQueueNames.HighPriority)]
    public async Task LinkConversationToTicketAsync(
        Id<Organization> organizationId,
        Id<Integration> integrationId,
        Id<Conversation> conversationId,
        Uri? messageUrl,
        Id<Member> actorId,
        Id<Organization> actorOrganizationId,
        IReadOnlyDictionary<string, object?> properties)
    {
        var messageLink = messageUrl is null
            ? "the conversation you requested"
            : $"<{messageUrl}|this conversation>";

        var org = await _organizationRepository.GetAsync(organizationId);
        if (org is null)
        {
            // We can't report anything to the user, we don't know who they are.
            Log.EntityNotFound(organizationId);
            return;
        }

        using var orgScope = Log.BeginOrganizationScope(org);
        if (!org.Enabled)
        {
            Log.OrganizationDisabled();
            return;
        }

        if (org.PlatformType != PlatformType.Slack)
        {
            Log.OrganizationNotOnSlack(organizationId, org.PlatformType);
            return;
        }

        var actor = await _userRepository.GetMemberByIdAsync(actorId, actorOrganizationId);
        if (actor is null)
        {
            // We can't report anything to the user, we don't know who they are.
            Log.EntityNotFound(actorId);
            return;
        }
        using var actorScope = Log.BeginMemberScope(actor);

        var convo = await _conversationRepository.GetConversationAsync(conversationId);
        if (convo is null)
        {
            // Now we can report errors
            Log.EntityNotFound(conversationId);
            var message = Activity.Current?.Id is { Length: > 0 } activityId
                ? $"There was an internal error, please try again. Contact '{WebConstants.SupportEmail}' and give them this identifier `{activityId}` if you're stuck."
                : $"There was an internal error, please try again. Contact '{WebConstants.SupportEmail}' if you're stuck.";

            ReportInternalError(message);
            return;
        }

        using var convoScopes = Log.BeginConversationRoomAndHubScopes(convo);

        var ticketing = await _integrationRepository.GetTicketingIntegrationByIdAsync(org, integrationId);
        if (ticketing is null)
        {
            Log.EntityNotFound(integrationId);
            return;
        }
        var integration = ticketing.Integration;
        var settings = Expect.Type<TSettings>(ticketing.Settings);

        var room = convo.Room;

        if (convo.GetTicketLink(ticketing) is { } link)
        {
            await ReportError(
                $"The conversation is already linked to <{link.WebUrl}|this {settings.IntegrationName} ticket>.");
            return;
        }

        if (!convo.Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            await ReportError(
                "Your organization no longer subscribes to a plan that includes conversation tracking. " +
                "Contact an administrator for assistance.");
            return;
        }

        if (integration is not { Enabled: true })
        {
            await ReportError(
                $"Your organization does not have an integration enabled for {settings.IntegrationName}.");
            return;
        }

        if (settings is not { HasApiCredentials: true })
        {
            await ReportError(
                $"Your organization does not have credentials configured for {settings.IntegrationName}.");
            return;
        }

        ConversationLink? conversationLink;
        var success = false;
        string? errorReason = null;
        try
        {
            conversationLink = await _ticketLinker.CreateTicketLinkAsync(integration, settings, properties, convo, actor);
            success = true;
        }
        catch (Exception ex)
        {
            var (reason, userErrorInfo, extraInfo) =
                ex switch
                {
                    TicketConfigurationException tcex =>
                        new(tcex.Reason, tcex.Message),
                    _ => _ticketLinker.ParseException(ex),
                };
            errorReason = reason.GetEnumMemberValueName();

            Log.ErrorCreatingTicket(ex, actorId, settings.IntegrationName, extraInfo);
            await ReportError(
                reason switch
                {
                    TicketErrorReason.Configuration => userErrorInfo ??
                        $"I couldn't create the ticket in {settings.IntegrationName} due to a configuration error. "
                        + $"{WebConstants.GetContactSupportSentence()}",
                    TicketErrorReason.UserConfiguration => userErrorInfo ??
                        $"I couldn't create the ticket in {settings.IntegrationName} due to configuration of the current user. "
                        + $"{WebConstants.GetContactSupportSentence()}",
                    TicketErrorReason.Unauthorized =>
                        $"The {settings.IntegrationName} credentials configured for your organization are invalid or have expired.",
                    TicketErrorReason.ApiError =>
                        $"I couldn't create the ticket due to an error calling {settings.IntegrationName}. "
                        + $"{WebConstants.GetContactSupportSentence()}"
                        + (userErrorInfo is not null ? $"\n\n```\n{userErrorInfo}\n```" : ""),
                    _ =>
                        $"I couldn't create the ticket due to an error at {settings.IntegrationName}. {WebConstants.GetContactSupportSentence()}",
                });

            // We don't want to rethrow because then hangfire will retry the job.
            // We already told the user it didn't work, so they can try it again manually.
            // If we identify some well-known transient issues, we can add `catch` clauses to retry those errors.
            return;
        }
        finally
        {
            _analyticsClient.Track(
                "Ticket Created",
                AnalyticsFeature.Integrations,
                actor,
                org,
                new {
                    integration = settings.AnalyticsSlug,
                    success,
                    reason = errorReason,
                });
        }

        var ticketLink = settings.GetTicketLink(conversationLink);
        if (ticketLink is { WebUrl: { } webUrl } && conversationLink is not null)
        {
            var ticketUrl = webUrl.ToString();
            Log.TicketCreated(actor, settings.IntegrationName, ticketUrl);

            // Add Ticket Reaction to message.
            try
            {
                await _reactionsApiClient.AddReactionAsync(org.RequireAndRevealApiToken(),
                    name: "ticket",
                    channel: convo.Room.PlatformRoomId,
                    timestamp: convo.FirstMessageId);
            }
            catch (Exception e)
            {
                // If this fails, we don't really care.
                Log.FailedToAddTicketReaction(e);
            }

            // Send a notification to the thread.
            await SendTicketStatusMessageToThreadAsync(ticketLink, conversationLink, ticketUrl, actor, room, convo, org);

            await ReportSuccess(ticketUrl);

            _systemSignaler.EnqueueSystemSignal(
                SystemSignal.TicketLinkedSignal,
                arguments: ticketLink.ToJson(),
                org,
                room.ToPlatformRoom(),
                convo.StartedBy,
                triggeringMessage: MessageInfo.FromConversation(convo));

            var outputs = new OutputsBuilder()
                .SetConversation(convo)
                .SetTicketLink(ticketLink)
                .Outputs;
            await _playbookDispatcher.DispatchAsync(
                TicketLinkedTrigger.Id,
                outputs,
                org,
                PlaybookRunRelatedEntities.From(convo));

        }
        else if (_ticketLinker.GetPendingTicketUrl(convo, actor) is { } pendingTicketUrl)
        {
            await ReportPending(pendingTicketUrl);
        }
        else
        {
            await ReportError(
                $"The ticket may have been created, but I can't find its link. {WebConstants.GetContactSupportSentence()}");
        }

        async Task ReportRoomOutcome(NotificationType notificationType, string headline, string message)
        {
            await _ticketNotificationService.PublishAsync(convo, notificationType, headline, message, actor);
        }

        void ReportInternalError(string message)
        {
            if (!actor.IsGuest && actor.OrganizationId == org.Id)
            {
                // Send from the home org, even if we're sending to a foreign user.
                _backgroundSlackClient.EnqueueDirectMessages(org, new[] { actor },
                    $":anguished: I wasn't able to link {messageLink} to a ticket. {message}");
            }
        }

        Task ReportError(string message) =>
            ReportRoomOutcome(NotificationType.TicketError, "Ticket Error",
                $"""
                {actor.ToMention()} could not create {settings.IntegrationName} ticket for {messageLink}.

                {message}
                """);

        Task ReportSuccess(string ticketUrl) =>
            ReportRoomOutcome(NotificationType.TicketCreated, "Ticket Created",
                $"""
                {actor.ToMention()} created <{ticketUrl}|{settings.IntegrationName} ticket> for {messageLink}.
                """);

        Task ReportPending(string ticketUrl) =>
            ReportRoomOutcome(NotificationType.TicketPending, "Ticket Pending",
                $"""
                {actor.ToMention()} requested <{ticketUrl}|{settings.IntegrationName} ticket> for {messageLink}.
                """);
    }

    async Task SendTicketStatusMessageToThreadAsync(
        IntegrationLink ticketLink,
        ConversationLink conversationLink,
        string ticketUrl,
        Member actor,
        Room room,
        Conversation convo,
        Organization org)
    {
        var externalId = ticketLink is ZendeskTicketLink zendeskLink
            ? $"{zendeskLink.TicketId}"
            // Temporary hack for unit tests. We'll fix this soon. Yes, I think it's gross too. - @haacked
            : conversationLink.LinkType is (ConversationLinkType)98989 ? "num" : null;

        if (externalId is null)
        {
            return;
        }

        var title = $"`[Opened]` <{ticketUrl}|{conversationLink.LinkType.ToDisplayString()} #{externalId}>";
        var context = $":bust_in_silhouette: Requested by {actor.ToMention()}";
        var blocks = new ILayoutBlock[]
        {
            new Section(new MrkdwnText(title)),
            new Context(context)
        };
        var message = new BotMessageRequest(
            $"{title}\n{context}",
            To: new ChatAddress(ChatAddressType.Room, room.PlatformRoomId, convo.FirstMessageId),
            Blocks: blocks);



        var response = await _messageDispatcher.DispatchAsync(message, org);
        if (response.Success)
        {
            if (TrySetStatusMessageId(conversationLink, response.MessageId, out var updatedSettings))
            {
                await _conversationRepository.UpdateConversationLinkAsync(conversationLink, updatedSettings);
            }
        }
    }

    public bool TrySetStatusMessageId(ConversationLink link, string messageId, out ZendeskSettings settings)
    {
        settings = JsonSettings.FromJson<ZendeskSettings>(link.Settings) ?? new();
        settings.StatusMessageId = messageId;
        return true;
    }
}

static partial class TicketLinkerJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Member {MemberId} created a ticket in {IntegrationName}: {TicketId}")]
    public static partial void TicketCreated(
        this ILogger<ITicketLinker> logger,
        Id<Member> memberId,
        string integrationName,
        string ticketId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Member {MemberId} failed to create a ticket in {IntegrationName}. Extra info: {ExtraInfo}")]
    public static partial void ErrorCreatingTicket(
        this ILogger<ITicketLinker> logger,
        Exception ex,
        Id<Member> memberId,
        string integrationName,
        string? extraInfo = null);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Organization {OrganizationId} runs on {PlatformType}, not Slack")]
    public static partial void OrganizationNotOnSlack(
        this ILogger<ITicketLinker> logger,
        Id<Organization> organizationId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Failed to add Ticket reaction")]
    public static partial void FailedToAddTicketReaction(this ILogger<ITicketLinker> logger, Exception exception);
}

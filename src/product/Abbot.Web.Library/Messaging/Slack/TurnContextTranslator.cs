using System.Collections.Generic;
using System.Linq;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Factory for creating a Slack <see cref="PlatformMessage" /> based on the incoming Bot Framework
/// message <see cref="ITurnContext"/> from Slack by way of Azure Bot Service.
/// </summary>
public class TurnContextTranslator : ITurnContextTranslator
{
    static readonly ILogger<TurnContextTranslator> Log =
        ApplicationLoggerFactory.CreateLogger<TurnContextTranslator>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IRoomRepository _roomRepository;
    readonly ISlackResolver _slackResolver;
    readonly ISlackIntegration _slackIntegration;
    readonly IIntegrationRepository _integrationRepository;
    readonly ISlackApiClient _slackApiClient;

    /// <summary>
    /// Constructs a <see cref="TurnContextTranslator"/> with the given <see cref="ISlackApiClient"/> we
    /// can use to make API calls to Slack and the given <see cref="IOrganizationRepository"/>
    /// used to query the organization associated with the message.
    /// </summary>
    /// <param name="organizationRepository">The <see cref="IOrganizationRepository"/> used to get information about the current organization.</param>
    /// <param name="roomRepository">The <see cref="IRoomRepository"/> used to get information about rooms.</param>
    /// <param name="slackResolver">A <see cref="ISlackResolver"/> used to resolve rooms, mentions, etc.</param>
    /// <param name="slackIntegration">The custom Slack integration service.</param>
    /// <param name="integrationRepository">The integration repository.</param>
    /// <param name="slackApiClient">The Slack API Client.</param>
    public TurnContextTranslator(
        IOrganizationRepository organizationRepository,
        IRoomRepository roomRepository,
        ISlackResolver slackResolver,
        ISlackIntegration slackIntegration,
        IIntegrationRepository integrationRepository,
        ISlackApiClient slackApiClient)
    {
        _organizationRepository = organizationRepository;
        _roomRepository = roomRepository;
        _slackResolver = slackResolver;
        _slackIntegration = slackIntegration;
        _integrationRepository = integrationRepository;
        _slackApiClient = slackApiClient;
    }

    /// <summary>
    /// Creates a <see cref="PlatformMessage" /> based on the incoming Bot Framework
    /// message <see cref="ITurnContext"/> from Slack by way of Azure Bot Service.
    /// </summary>
    /// <param name="turnContext">The <see cref="ITurnContext"/> representing a Bot Framework install event.</param>
    public async Task<IPlatformMessage?> TranslateMessageAsync(ITurnContext turnContext)
    {
        var channelData = GetSlackChannelData(turnContext);

        IElement? incomingEvent = channelData.SlackMessage ?? channelData.Payload;
        if (incomingEvent is null)
        {
            return LogErrorAndReturnNull(
                $"{nameof(TranslateMessageAsync)}: message does not contain a proper SlackMessage or Payload property.",
                channelData);
        }

        var message = incomingEvent switch
        {
            IEventEnvelope<MessageEvent> messageEnvelope => await CreateMessageAsync(turnContext, messageEnvelope),
            MessageBlockActionsPayload payload => await CreateMessageAsync(
                turnContext,
                payload,
                (text, _) => MessageEventInfo.FromSlackBlockActionsPayload(text, payload)),
            InteractiveMessagePayload payload => await CreateMessageAsync(
                turnContext,
                payload,
                (text, _) => MessageEventInfo.FromSlackInteractiveMessagePayload(text, payload)),
            MessageActionPayload payload => await CreateMessageAsync(
                turnContext,
                payload,
                (text, _) => MessageEventInfo.FromSlackMessageActionPayload(text, payload)),
            _ => null,
        };

        if (message is { Room: { } room, From.User.IsBot: false })
        {
            // Track room activity.
            await _roomRepository.UpdateLastMessageActivityAsync(room);
        }

        return message;
    }

    /// <summary>
    /// Returns an <see cref="Serious.Abbot.Events.IPlatformEvent" /> when Abbot is installed.
    /// </summary>
    /// <param name="turnContext">The incoming install event.</param>
    public async Task<InstallEvent> TranslateInstallEventAsync(ITurnContext turnContext)
    {
        var channelData = turnContext.GetChannelData<SlackChannelData>();

        if (channelData.SlackMessage is not BotAddedEvent installEvent)
        {
            throw new InvalidOperationException(
                "Attempted to translate an install event that was not an app_installed event.");
        }

        // In this one case, it's safe to grab the platformId from the `From` user because
        // a user from another org could not install the bot.
        var platformId = turnContext.Activity.From.Id.RightAfterLast(':');

        var apiToken = channelData.ApiToken;
        string botId = installEvent.Bot.Id;

        return await _slackResolver.ResolveInstallEventAsync(apiToken, platformId, botId, installEvent.Bot.AppId);
    }

    public async Task<IPlatformEvent?> TranslateUninstallEventAsync(ITurnContext turnContext)
    {
        var channelData = GetSlackChannelData(turnContext);

        if (channelData.SlackMessage is not IEventEnvelope<AppUninstalledEvent> uninstallEvent)
        {
            throw new InvalidOperationException(
                "Attempted to translate an uninstall event that was not an app_uninstalled event.");
        }

        var platformId = uninstallEvent.TeamId
                         ?? throw new InvalidOperationException("Somehow the uninstall event had no team id.");

        return await ToPlatformEvent(
            uninstallEvent,
            new UninstallPayload(platformId, uninstallEvent.ApiAppId.Require()),
            turnContext);
    }

    public async Task<IPlatformEvent?> TranslateEventAsync(ITurnContext turnContext)
    {
        var channelData = GetSlackChannelData(turnContext);
        var slackMessage = channelData.SlackMessage ?? channelData.Payload;
        return slackMessage switch
        {
            IPayload payload => await ToPlatformEvent(payload, payload, turnContext),
            BotAddedEvent => throw new InvalidOperationException(
                $"Attempted to translate a non-install event, but it was an app_installed event. Call {nameof(TranslateInstallEventAsync)} instead."),
            IEventEnvelope<EventBody> envelope => await ToPlatformEventAsync(envelope, turnContext),
            null => LogErrorAndReturnNull(
                $"{nameof(TranslateEventAsync)}: message does not contain a proper SlackMessage or Payload property.",
                channelData),
            _ => null
        };
    }

    const string RemovedMessagePrefix = "You have been removed from #";

    async Task<RoomMembershipEventPayload?> CreateRoomMembershipPayloadAsync(
        MessageEvent messageEvent,
        IEnumerable<Authorization> authorizations,
        Organization organization)
    {
        var messageText = messageEvent.Text;
        if (messageText is null or "")
        {
            return null;
        }
        var indexOfRoomName = messageText.IndexOf("You have been removed from #", StringComparison.Ordinal);
        var indexOfBy = messageText.IndexOf(" by \u003c@", StringComparison.Ordinal);

        if (indexOfRoomName < 0 || indexOfBy < 0 || indexOfBy <= indexOfRoomName)
        {
            return null;
        }

        var roomName = messageText[RemovedMessagePrefix.Length..(indexOfBy - indexOfRoomName)];

        var room = await _roomRepository.GetRoomByNameAsync(roomName, organization);
        if (room is null)
        {
            // Room could be null if our record for the name is out of date. However, that is a relatively
            // unlikely scenario, so for now we'll ignore it. If it becomes a problem, we can set the PlatformRoomId
            // to an empty string in this scenario and have the RoomMembershipPayloadHandler special case a full
            // update if the room id is an empty string.
            return null;
        }


        var botUserId = authorizations.FirstOrDefault()?.UserId;
        if (botUserId is null)
        {
            return null;
        }

        return new RoomMembershipEventPayload(
            MembershipChangeType.Removed,
            room.PlatformRoomId,
            botUserId);
    }

    static IPlatformMessage? LogErrorAndReturnNull(string message, object? channelData)
    {
        Log.ErrorDeserializingSlackMessage(message, channelData);
        return null;
    }

    async Task<PlatformMessage?> CreateMessageAsync(
        ITurnContext turnContext,
        IEventEnvelope<MessageEvent> messageEnvelope)
    {
        return await CreateMessageAsync(
            turnContext,
            messageEnvelope,
            (text, org) =>
                org.PlatformBotUserId is null
                    ? null
                    : MessageEventInfo.FromSlackMessageEvent(text, messageEnvelope.Event, org.PlatformBotUserId));
    }

    async Task<PlatformMessage?> CreateMessageAsync(
        ITurnContext turnContext,
        IElement element,
        Func<string, Organization, MessageEventInfo?> messageEventPayloadAccessor)
    {
        var (organization, bot) = await GetOrganizationAndBotUser(element, turnContext);
        if (organization is null || bot is null)
        {
            return null;
        }

        var activity = turnContext.Activity;
        var text = activity.Text ?? string.Empty;

        var messageEventPayload = messageEventPayloadAccessor(text, organization);
        if (messageEventPayload is null)
        {
            return null;
        }

        var roomId = messageEventPayload.PlatformRoomId;
        var fromId = messageEventPayload.PlatformUserId;
        var from = await _slackResolver.ResolveMemberAsync(fromId, organization)
                   ?? throw new InvalidOperationException(
                       $"Could not resolve the Slack sender {fromId} of the message.");

        var mentioned = messageEventPayload.MentionedUserIds.Any()
            ? await ResolveMentionsAsync(messageEventPayload.MentionedUserIds, organization)
            : Array.Empty<Member>();

        var room = await _slackResolver.ResolveRoomAsync(roomId, organization, false);

        var messageUrl = messageEventPayload.MessageId is { } ts
            ? SlackFormatter.MessageUrl(organization.Domain,
                roomId,
                ts,
                messageEventPayload.ThreadId)
            : null;

        return new PlatformMessage(
            messageEventPayload,
            messageUrl,
            organization,
            (activity.Timestamp ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            new Responder(_slackApiClient, turnContext, bot),
            from,
            bot,
            mentioned,
            room);
    }

    static SlackChannelData GetSlackChannelData(ITurnContext turnContext)
    {
        var activity = turnContext.Activity;

        return activity.ChannelData switch
        {
            Payload interactionPayload => new SlackChannelData
            {
                Payload = interactionPayload
            },
            IEventEnvelope<EventBody> eventEnvelope => new SlackChannelData
            {
                SlackMessage = eventEnvelope
            },
            JObject jObject => jObject.ToObject<SlackChannelData>() ?? throw new InvalidOperationException(
                $"Slack ChannelData could not be cast to {nameof(SlackChannelData)}."),
            _ => throw new InvalidOperationException(
                $"Slack ChannelData is not a JObject as expected. It is {activity.ChannelData.GetType()}")
        };
    }

    async Task<IPlatformEvent?> ToPlatformEventAsync(IEventEnvelope<EventBody>? envelope, ITurnContext turnContext)
    {
        if (envelope is null)
        {
            return null;
        }

        return await ToPlatformEvent(envelope, envelope.Event, turnContext);
    }

    async Task<IPlatformEvent?> ToPlatformEvent<TBody>(IElement element, TBody eventBody, ITurnContext turnContext)
    {
        var (organization, bot) = await GetOrganizationAndBotUser(element, turnContext);
        if (organization is null || bot is null)
        {
            return null;
        }

        var responder = new Responder(_slackApiClient, turnContext, bot);

        var fromUserId = eventBody switch
        {
            MessageChangedEvent { PreviousMessage.User: { Length: > 0 } userId } => userId,
            MessageDeletedEvent { PreviousMessage.User: { Length: > 0 } userId } => userId,
            MessageEvent when element is IEventEnvelope<MessageEvent> { Authorizations: { Count: > 0 } authorizations } => authorizations[0].UserId,
            EventBody { User: { } eventUserId } => eventUserId,
            UserChangeEvent { User.Id: { } userChangeUserId } => userChangeUserId,
            InteractionPayload { User.Id: { } interactionUserId } => interactionUserId,
            UninstallPayload => bot.UserId,
            _ => null
        };

        var from = (fromUserId is not null
            ? await _slackResolver.ResolveMemberAsync(fromUserId, organization)
            : null) ?? await _organizationRepository.EnsureAbbotMember(organization);

        IPlatformEvent<TPayload> CreateEvent<TPayload>(TPayload payload, string? triggerId = null)
        {
            return new PlatformEvent<TPayload>(
                payload,
                triggerId,
                bot,
                DateTimeOffset.UtcNow,
                responder,
                from,
                null,
                organization);
        }

        async Task<PlatformEvent<TPayload>?> CreateRoomEventAsync<TPayload>(TPayload payload, string? channel)
        {
            var room = channel is not null
                ? await _slackResolver.ResolveRoomAsync(channel, organization, false)
                : null;

            if (room is null)
            {
                return null;
            }

            return new PlatformEvent<TPayload>(
                payload,
                null,
                bot,
                DateTimeOffset.UtcNow,
                responder,
                from,
                room,
                organization);
        }

        return eventBody switch
        {
            ReactionAddedEvent { Item.Channel: { Length: > 0 } channel } reactionEvent => await CreateRoomEventAsync(
                reactionEvent,
                channel),
            ReactionRemovedEvent { Item.Channel: { Length: > 0 } channel } reactionEvent => await CreateRoomEventAsync(
                reactionEvent,
                channel),
            SharedChannelInviteApproved payload => CreateEvent(payload),
            SharedChannelInviteAccepted payload => CreateEvent(payload),
            SharedChannelInviteDeclined payload => CreateEvent(payload),
            BlockSuggestionPayload payload => CreateEvent(payload),
            IViewClosedPayload payload => CreateEvent(payload),
            IViewSubmissionPayload payload => CreateEvent(payload, payload.TriggerId),
            IViewBlockActionsPayload payload => CreateEvent(payload, payload.TriggerId),
            UserChangeEvent userChangeEvent => CreateEvent(UserEventPayload.FromSlackUserInfo(userChangeEvent.User)),
            TeamRenameEvent teamRenameEvent => CreateEvent(TeamChangeEventPayload.FromTeamRenameEvent(teamRenameEvent)),
            TeamDomainChangeEvent teamDomainEvent => CreateEvent(
                TeamChangeEventPayload.FromTeamDomainChangeEvent(teamDomainEvent)),
            AppHomeOpenedEvent appHomeOpenedEvent => CreateEvent(appHomeOpenedEvent),
            UninstallPayload uninstallPayload => CreateEvent(uninstallPayload),
            ChannelRenameEvent channelRenameEvent => CreateEvent(new RoomEventPayload(channelRenameEvent.Channel.Id)),
            ChannelRenameMessageEvent { Channel: { } } channelRename => CreateEvent(
                new RoomEventPayload(channelRename.Channel)),
            MemberJoinedChannelEvent memberJoinedEvent => CreateEvent(new RoomMembershipEventPayload(
                MembershipChangeType.Added,
                memberJoinedEvent.Channel,
                memberJoinedEvent.User.Require(),
                memberJoinedEvent.Inviter)),
            MemberLeftChannelEvent memberLeftChannelEvent => CreateEvent(new RoomMembershipEventPayload(
                MembershipChangeType.Removed,
                memberLeftChannelEvent.Channel,
                memberLeftChannelEvent.User.Require())),
            ChannelLeftEvent channelLeftEvent => CreateEvent(new RoomMembershipEventPayload(
                MembershipChangeType.Removed,
                channelLeftEvent.Channel,
                organization.PlatformBotUserId!)), // checked in GetOrganization()
            ChannelLifecycleEvent channelLifecycleEvent => CreateEvent(
                new RoomEventPayload(channelLifecycleEvent.Channel)),
            MessageChangedEvent messageChangedEvent => await CreateRoomEventAsync(messageChangedEvent,
                messageChangedEvent.Channel),
            MessageDeletedEvent messageDeletedEvent => await CreateRoomEventAsync(messageDeletedEvent,
                messageDeletedEvent.Channel),
            MessageEvent { SubType: "channel_convert_to_private" } messageEvent => CreateEvent(
                new RoomEventPayload(messageEvent.Channel.Require())),
            // SlackAdapter should already filter out these subtypes, but just in case.
            MessageEvent { SubType.Length: > 0 } messageEvent when !SlackAdapter.IsAllowedMessageEvent(messageEvent) =>
                null,
            MessageEvent { User: "USLACKBOT" } messageEvent
                when element is IEventEnvelope<MessageEvent> { Authorizations: { Count: > 0 } authorizations }
                => await CreateRoomMembershipPayloadAsync(messageEvent, authorizations, organization) is { } payload
                    ? CreateEvent(payload)
                    : null,
            MessageEvent =>
#if DEBUG
                throw new InvalidOperationException(
                    $"Call {nameof(TranslateMessageAsync)} instead.\n{JsonConvert.SerializeObject(eventBody, Formatting.Indented)}"),
#else
                throw new InvalidOperationException($"Call {nameof(TranslateMessageAsync)} instead."),
#endif
            var genericEvent => CreateEvent(genericEvent)
        };
    }

    async Task<(Organization?, SlackBotChannelUser?)> GetOrganizationAndBotUser(IElement element, ITurnContext turnContext)
    {
        var apiAppId = element.ApiAppId;
        Organization? organization = null;

        if (apiAppId is not null)
        {
            var (integration, _) = await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(apiAppId);
            if (integration is not null)
            {
                organization = integration.Organization;
            }
        }

        if (organization is null)
        {
            var platformId = element switch
            {
                IEventEnvelope { Authorizations: { Count: > 0 } authorizations }
                    when authorizations.Select(a => a.TeamId).WhereNotNull().FirstOrDefault() is { Length: > 0 } teamId => teamId,
                // If the event is a view payload, the TeamId will be the ID of the team of the user who caused the event.
                // That may be a foreign user, so we check for AppInstalledTeamId, which contains the TeamId for the team that
                // installed the app that created the view.
                IViewPayload { View.AppInstalledTeamId: { Length: > 0 } teamId } => teamId,
                _ => element.TeamId
            };

            organization = await _organizationRepository.GetAsync(platformId.Require());
        }

        if (organization is null)
        {
            return default;
        }

        var integrationId = turnContext.GetIntegrationId();
        var auth = await _slackIntegration.GetAuthorizationAsync(organization, integrationId);
        if (auth is { AppId: null } or { BotId: null } or { BotUserId: null })
        {
            return default;
        }

        // The authorized bot is not currently enabled...
        if (organization.BotAppId != auth.AppId)
        {
            // Should we ignore the event to avoid duplicates?
            if (IgnoreWhileBotDisabled(element))
            {
                // This request is not for us.
                return default;
            }
        }

        var botChannelUser = new SlackBotChannelUser(
            platformId: organization.PlatformId,
            botId: auth.BotId,
            botUserId: auth.BotUserId,
            displayName: auth.BotName,
            botResponseAvatar: organization.BotResponseAvatar,
            apiToken: auth.ApiToken,
            scopes: auth.Scopes);

        return (organization, botChannelUser);
    }

    static bool IgnoreWhileBotDisabled(IElement element) =>
        element switch
        {
            // TODO: Teach events if they should be processed for disabled apps?
            IEventEnvelope<AppUninstalledEvent> => false,
            IEventEnvelope<AppHomeOpenedEvent> => false,
            IInteractionPayload => false,
            _ => true,
        };

    async Task<IReadOnlyList<Member>> ResolveMentionsAsync(IEnumerable<string> mentionedUserIds,
        Organization organization)
    {
        Task<Member?> ResolveMentionAsync(string userId)
        {
            return _slackResolver.ResolveMemberAsync(userId, organization);
        }

        // IMPORTANT: We need to do these one at a time because to avoid DbContext threading issues:
        // https://docs.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues
        // Love, @haacked - 2022/02/14
        var mentioned = new List<Member>();
        foreach (var userId in mentionedUserIds)
        {
            var member = await ResolveMentionAsync(userId);
            if (member is not null)
            {
                mentioned.Add(member);
            }
        }

        return mentioned.ToReadOnlyList();
    }
}

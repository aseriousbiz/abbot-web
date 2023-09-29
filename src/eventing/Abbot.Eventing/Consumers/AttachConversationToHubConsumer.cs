using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Eventing;

public class AttachConversationToHubConsumer : IConsumer<AttachConversationToHub>, IConsumer<NewConversation>
{
    readonly ILogger<AttachConversationToHubConsumer> _logger;
    readonly ISlackApiClient _slackApiClient;
    readonly HubMessageRenderer _hubMessageRenderer;
    readonly IConversationRepository _conversationRepository;
    readonly IOrganizationRepository _organizationRepository;
    readonly IHubRepository _hubRepository;
    readonly IUserRepository _userRepository;
    readonly IClock _clock;

    public AttachConversationToHubConsumer(IConversationRepository conversationRepository,
        IOrganizationRepository organizationRepository, IHubRepository hubRepository,
        IUserRepository userRepository, IClock clock, ILogger<AttachConversationToHubConsumer> logger,
        ISlackApiClient slackApiClient, HubMessageRenderer hubMessageRenderer)
    {
        _logger = logger;
        _slackApiClient = slackApiClient;
        _hubMessageRenderer = hubMessageRenderer;
        _conversationRepository = conversationRepository;
        _organizationRepository = organizationRepository;
        _hubRepository = hubRepository;
        _userRepository = userRepository;
        _clock = clock;
    }

    public async Task Consume(ConsumeContext<NewConversation> context)
    {
        if (context.Message.RoomHubId is not { } roomHubId)
        {
            // Ignore this conversation
            return;
        }

        // Fetch pre-requisite data
        var organization = context.GetPayload<Organization>();

        var conversation = await _conversationRepository.GetConversationAsync(context.Message.ConversationId);
        if (conversation is null)
        {
            _logger.EntityNotFound(context.Message.ConversationId);
            return;
        }

        // There's a small chance that we end up running for a conversation that's already attached to a hub.
        if (conversation.HubId is not null)
        {
            // No-op, the conversation is already attached.
            // We already check this condition before even publishing the message so getting here means we really lost a race.
            return;
        }

        conversation.RequireParent(organization);
        using var convoScope = _logger.BeginConversationRoomAndHubScopes(conversation);

        var hub = await _hubRepository.GetHubByIdAsync(roomHubId);
        if (hub is null)
        {
            _logger.EntityNotFound(roomHubId);
            return;
        }
        using var hubScope = _logger.BeginHubScope(hub);

        // In this case, Abbot is doing the routing because the conversation is being _automatically_ routed.
        var actor = await _organizationRepository.EnsureAbbotMember(organization);

        await AttachConversationToHubAsync(context, organization, conversation, hub, actor);
    }

    public async Task Consume(ConsumeContext<AttachConversationToHub> context)
    {
        // Fetch pre-requisite data
        var organization = context.GetPayload<Organization>();

        var conversation = await _conversationRepository.GetConversationAsync(context.Message.ConversationId);
        if (conversation is null)
        {
            _logger.EntityNotFound(context.Message.ConversationId);
            return;
        }
        conversation.RequireParent(organization);

        // There's a small chance that we end up running for a conversation that's already attached to a hub.
        if (conversation.HubId is not null)
        {
            // No-op, the conversation is already attached.
            // We already check this condition before even publishing the message so getting here means we really lost a race.
            return;
        }

        using var convoScope = _logger.BeginConversationRoomAndHubScopes(conversation);

        var hub = await _hubRepository.GetHubByIdAsync(context.Message.HubId);
        if (hub is null)
        {
            _logger.EntityNotFound(context.Message.HubId);
            return;
        }
        using var hubScope = _logger.BeginHubScope(hub);

        var actor = await _userRepository.GetMemberByIdAsync(context.Message.ActorMemberId,
            context.Message.ActorOrganizationId);
        if (actor is null)
        {
            _logger.EntityNotFound(context.Message.ActorMemberId);
            return;
        }
        using var actorScope = _logger.BeginMemberScope(actor);

        await AttachConversationToHubAsync(context, organization, conversation, hub, actor);
    }

    async Task AttachConversationToHubAsync(ConsumeContext context, Organization organization, Conversation conversation, Hub hub, Member actor)
    {
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            _logger.OrganizationHasNoSlackApiToken();
            return;
        }

        // Create a Hub Thread for the conversation
        // We do this first so we can make sure the set all the values we need to set in the database at once.
        // However, this does open us up to a risk: A Hub Thread is created, but the database transaction fails.
        // For now, we're not handling that case.
        var message = await _hubMessageRenderer.RenderHubThreadRootAsync(
                conversation) with
        {
            Channel = hub.Room.PlatformRoomId,
        };
        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, message);
        if (!response.Ok)
        {
            _logger.FailedToAttachConversationToHub("Error posting hub thread: {response.Error}");
            return;
        }

        _logger.CreatedHubThread(response.Timestamp);

        // There's a Distributed Transaction problem here that I'm punting for now.
        // If the database transaction fails, we'll have a dangling Slack message to deal with.
        //
        // We could do the Saga thing and write compensation logic to delete the message, but that's complex.
        //
        // We could also do a three-phase thing (no, not three-phase commit... well... not exactly 🤪) where we:
        // 1. Set HubId on the conversation
        // 2. Create the Hub Thread
        // 3. Set HubThreadId on the conversation
        // But that feels messy and it also leaves a bunch of open questions (when/how do we post the AttachedToHub conversation event?).
        //
        // So. We're punting for now.
        // There's a risk that we'll end up with a dangling Slack message if the database transaction fails.

        // Create the link
        var result =
            await _conversationRepository.AttachConversationToHubAsync(conversation,
                hub,
                response.Timestamp,
                SlackFormatter.MessageUrl(organization.Domain, hub.Room.PlatformRoomId, response.Timestamp),
                actor,
                _clock.UtcNow);

        if (!result.IsSuccess)
        {
            // Conflict errors are fine. They just indicate someone else put the same trigger emoji on the message.
            if (result.Type != EntityResultType.Conflict)
            {
                _logger.FailedToAttachConversationToHub(result.ErrorMessage);
            }

            return;
        }

        _logger.AttachedConversationToHub();
    }
}

static partial class AssignConversationToHubConsumerLoggerExtensions
{
    [LoggerMessage(EventId = 1,
        Level = LogLevel.Debug,
        Message = "Attached conversation to hub")]
    public static partial void AttachedConversationToHub(this ILogger<AttachConversationToHubConsumer> logger);

    [LoggerMessage(EventId = 2,
        Level = LogLevel.Error,
        Message = "Failed to attach conversation to hub: {ErrorMessage}")]
    public static partial void FailedToAttachConversationToHub(this ILogger<AttachConversationToHubConsumer> logger,
        string errorMessage);

    [LoggerMessage(EventId = 3,
        Level = LogLevel.Information,
        Message = "Created Hub Thread '{HubThreadId}'")]
    public static partial void CreatedHubThread(this ILogger<AttachConversationToHubConsumer> logger,
        string hubThreadId);
}

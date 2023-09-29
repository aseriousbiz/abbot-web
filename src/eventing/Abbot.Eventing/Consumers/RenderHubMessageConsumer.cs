using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Eventing;

public class RenderHubMessageConsumer : IConsumer<RefreshHubMessage>
{
    readonly IConversationRepository _conversationRepository;
    readonly IHubRepository _hubRepository;
    readonly HubMessageRenderer _hubMessageRenderer;
    readonly ISlackApiClient _slackApiClient;
    readonly ILogger<RenderHubMessageConsumer> _logger;

    public RenderHubMessageConsumer(
        IConversationRepository conversationRepository,
        IHubRepository hubRepository,
        HubMessageRenderer hubMessageRenderer,
        ISlackApiClient slackApiClient,
        ILogger<RenderHubMessageConsumer> logger)
    {
        _conversationRepository = conversationRepository;
        _hubRepository = hubRepository;
        _hubMessageRenderer = hubMessageRenderer;
        _slackApiClient = slackApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RefreshHubMessage> context)
    {
        var organization = context.GetPayload<Organization>();

        var conversation = await _conversationRepository.GetConversationAsync(context.Message.ConversationId);
        if (conversation is null)
        {
            _logger.EntityNotFound(context.Message.ConversationId);
            return;
        }
        conversation.RequireParent(organization);

        if (conversation.HubId is not { } hubId || conversation.HubThreadId is not { Length: > 0 } hubThreadId)
        {
            // Not sure why, but we have a conversation with no hub thread.
            // So we can't re-render anything.
            return;
        }

        var hub = await _hubRepository.GetHubByIdAsync(new(hubId));
        if (hub is null)
        {
            _logger.EntityNotFound(hubId, typeof(Hub));
            return;
        }

        if (!organization.TryGetUnprotectedApiToken(out var slackToken))
        {
            _logger.OrganizationHasNoSlackApiToken();
            return;
        }

        // Re-render the message
        var message = await _hubMessageRenderer.RenderHubThreadRootAsync(conversation)
            with
        {
            Channel = hub.Room.PlatformRoomId,
            Timestamp = hubThreadId
        };
        await _slackApiClient.UpdateMessageAsync(slackToken, message);
    }
}

using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Signals;

namespace Serious.Abbot.Eventing;

/// <summary>
/// Listens for new messages and raises appropriate signals based on the categories attached to the message.
/// </summary>
public class CategorySignalingConsumer : IConsumer<NewMessageInConversation>
{
    readonly ISystemSignaler _signaler;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly FeatureService _featureService;
    readonly ILogger<CategorySignalingConsumer> _logger;

    public CategorySignalingConsumer(
        ISystemSignaler signaler,
        PlaybookDispatcher playbookDispatcher,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        FeatureService featureService,
        ILogger<CategorySignalingConsumer> logger)
    {
        _signaler = signaler;
        _playbookDispatcher = playbookDispatcher;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _featureService = featureService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NewMessageInConversation> context)
    {
        if (!context.Message.IsLive)
        {
            return;
        }
        await RaiseSignalsFromCategoriesAsync(
            context.GetPayload<Organization>(),
            context.Message.MessageId,
            context.Message.MessageText,
            context.Message.ClassificationResult,
            context.Message.MessageUrl,
            context.Message.ConversationId,
            context.Message.SenderId);
    }

    async Task RaiseSignalsFromCategoriesAsync(
        Organization organization,
        string? messageId,
        string? messageText,
        ClassificationResult? classificationResult,
        Uri? messageUrl,
        Id<Conversation> conversationId,
        Id<Member>? senderId)
    {
        if (classificationResult is null)
        {
            return;
        }

        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation is null)
        {
            _logger.EntityNotFound(conversationId);
            return;
        }
        conversation.RequireParent(organization);
        using var convoScopes = _logger.BeginConversationRoomAndHubScopes(conversation);

        if (organization.Settings.AIEnhancementsEnabled is not true)
        {
            return;
        }

        if (!await _featureService.IsEnabledAsync(FeatureFlags.AIEnhancements, organization))
        {
            return;
        }

        var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

        await _conversationRepository.AddTimelineEventAsync(
            conversation,
            abbot,
            classificationResult.UtcTimestamp,
            new ConversationClassifiedEvent
            {
                MessageId = messageId,
                Metadata = classificationResult.ToJson(),
            }
        );

        foreach (var category in classificationResult.Categories)
        {
            var signal = SystemSignal.CreateSignalFromCategory(category);
            var messageInfo = new MessageInfo(
                messageId ?? conversation.FirstMessageId,
                messageText ?? conversation.Title,
                messageUrl,
                conversation.FirstMessageId,
                conversation,
                senderId ?? conversation.StartedBy);
            _signaler.EnqueueSystemSignal(
                signal,
                category.Value,
                conversation.Organization,
                conversation.Room.ToPlatformRoom(),
                conversation.StartedBy,
                messageInfo);

            // Special case for the playbook sentiment trigger.
            if (signal.Name == SystemSignal.ConversationCategorySentimentSignal.Name)
            {
                var outputs = new OutputsBuilder()
                    .SetConversation(conversation)
                    .SetMessage(conversation.Room, messageInfo)
                    .Outputs;
                outputs["sentiment"] = category.Value;
                await _playbookDispatcher.DispatchAsync(
                    SentimentTrigger.Id,
                    outputs,
                    organization,
                    PlaybookRunRelatedEntities.From(conversation));
            }
        }
    }
}

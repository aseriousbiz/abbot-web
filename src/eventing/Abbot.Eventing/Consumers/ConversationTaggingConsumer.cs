using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Live;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Eventing;

/// <summary>
/// Listens for new messages in conversations and updates the conversation's tags accordingly.
/// </summary>
public class ConversationTaggingConsumer : IConsumer<NewMessageInConversation>
{
    readonly ITagRepository _tagRepository;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly IFlashPublisher _flashPublisher;
    readonly ILogger<ConversationTaggingConsumer> _logger;

    public ConversationTaggingConsumer(
        ITagRepository tagRepository,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IFlashPublisher flashPublisher,
        ILogger<ConversationTaggingConsumer> logger)
    {
        _tagRepository = tagRepository;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _flashPublisher = flashPublisher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NewMessageInConversation> context)
    {
        await TagConversationAsync(
            context.GetPayload<Organization>(),
            context.Message.ConversationId,
            context.Message.ClassificationResult);
    }

    async Task TagConversationAsync(
        Organization organization,
        Id<Conversation> conversationId,
        ClassificationResult? classification)
    {
        // If there's no categories, do nothing.
        if (classification is not { Categories: { Count: > 0 } categories })
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

        var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

        Expect.True(conversation.Tags.IsLoaded);
        var existingTags = conversation.Tags.Select(ct => ct.Tag).ToList();
        var candidateTags = await _tagRepository.EnsureTagsAsync(
            categories.Select(c => c.ToString()),
            "AI Generated.",
            abbot,
            organization);
        var finalTags = existingTags.Concat(candidateTags).DistinctBy(t => t.Name).ToList();
        await _tagRepository.TagConversationAsync(conversation, finalTags.Select(t => t.Id), abbot.User);
        await _flashPublisher.PublishAsync(
            FlashName.ConversationListUpdated,
            FlashGroup.Organization(organization));
    }
}

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.AI;

/// <summary>
/// Builds a sanitized conversation history.
/// </summary>
public interface ISanitizedConversationHistoryBuilder
{
    /// <summary>
    /// Given a <see cref="Conversation"/>, looks at its past <see cref="MessagePostedEvent"/> instances and returns a
    /// a sanitized version of the history that can be used to build a prompt to send to chat gpt.
    /// </summary>
    /// <param name="conversation">The conversation to build a history for.</param>
    /// <returns>A <see cref="SanitizedConversationHistory"/>.</returns>
    Task<SanitizedConversationHistory?> BuildHistoryAsync(Conversation conversation);
}

public class SanitizedConversationHistoryBuilder : ISanitizedConversationHistoryBuilder
{
    static readonly ILogger<SanitizedConversationHistoryBuilder> Log =
        ApplicationLoggerFactory.CreateLogger<SanitizedConversationHistoryBuilder>();

    readonly IConversationRepository _conversationRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly ITextAnalyticsClient _textAnalyticsClient;

    public SanitizedConversationHistoryBuilder(
        IConversationRepository conversationRepository,
        ISlackApiClient slackApiClient,
        ITextAnalyticsClient textAnalyticsClient)
    {
        _conversationRepository = conversationRepository;
        _slackApiClient = slackApiClient;
        _textAnalyticsClient = textAnalyticsClient;
    }

    public async Task<SanitizedConversationHistory?> BuildHistoryAsync(Conversation conversation)
    {
        var timeline = await _conversationRepository.GetTimelineAsync(conversation);
        var messageEvents = timeline.OfType<MessagePostedEvent>()
            .Where(me => me.Metadata is not null)
            .ToList();

        if (timeline.Any())
        {
            var history = SanitizedConversationHistory.Sanitize(messageEvents);
            if (history.Messages.Any())
            {
                return history;
            }
        }

        // Uh oh, this is an older conversation before we started storing messages in MessagePostedEvent.
        // Let's build the message history the old way.
        return await BuildHistoryLegacyAsync(conversation);
    }

    async Task<SanitizedConversationHistory?> BuildHistoryLegacyAsync(Conversation conversation)
    {
        if (!conversation.Organization.TryGetUnprotectedApiToken(out var slackToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return null;
        }
        var response = await _slackApiClient.Conversations.GetConversationRepliesAsync(
            slackToken,
            conversation.Room.PlatformRoomId,
            conversation.FirstMessageId,
            limit: 1000); // If the conversation is more than 1000, nobody needs to know what's going on.

        if (!response.Ok)
        {
            Log.SlackErrorRetrievingReplies(response.Error);
            return null;
        }

        var participants = conversation.Members
            .Select(m => SourceUser.FromMemberAndRoom(m.Member, conversation.Room))
            .ToDictionary(m => m.Id, m => m);

        // Since we're grabbing these from the API, we need to sanitize them all and build up an aggregated
        // replacements dictionary, hence the use of `SelectWithPrevious`.
        var sanitizedReplies = await response.Body
            .Where(msg => msg is { Timestamp: not null, User: not null, Text: not null })
            .SelectWithPrevious(
                selector: async (msg, prev) => await GetSanitizedSourceMessage(msg, participants, prev.Replacements) ?? prev,
                seed: SanitizedSourceMessage.Empty)
            .ToListAsync();
        var sourceMessages = sanitizedReplies.Select(r => r.SourceMessage).ToList();
        return new SanitizedConversationHistory(sourceMessages, sanitizedReplies.Last().Replacements);
    }

    async Task<SanitizedSourceMessage?> GetSanitizedSourceMessage(
        SlackMessage message,
        IReadOnlyDictionary<string, SourceUser> usersLookup,
        IReadOnlyDictionary<string, SecretString> existingReplacements)
    {
        if (usersLookup.TryGetValue(message.User.Require(), out var sourceUser))
        {
            var messageText = message.Text.Require();
            var sensitiveValues = await _textAnalyticsClient.RecognizePiiEntitiesAsync(messageText);
            var sanitizedText = SensitiveDataSanitizer.Sanitize(
                messageText,
                sensitiveValues,
                existingReplacements: existingReplacements);
            var slackTimestamp = SlackTimestamp.Parse(message.Timestamp.Require());
            var sourceMessage = new SourceMessage(sanitizedText.Message, sourceUser, slackTimestamp);
            return new SanitizedSourceMessage(sourceMessage, sanitizedText.Replacements);
        }

        return null;
    }
}

static partial class SanitizedConversationHistoryBuilderLoggingExtensions
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Slack error retrieving conversation replies: {SlackError}")]
    public static partial void SlackErrorRetrievingReplies(
        this ILogger<SanitizedConversationHistoryBuilder> logger,
        string slackError);
}

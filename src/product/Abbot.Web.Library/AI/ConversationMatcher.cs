using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Microsoft.Extensions.Logging;
using OpenAI_API.Chat;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Serialization;
using Serious.Abbot.Services;
using Serious.Logging;
using Serious.Text;
using Conversation = Serious.Abbot.Entities.Conversation;

namespace Serious.Abbot.AI;

/// <summary>
/// Service used to find a conversation for a given message.
/// </summary>
public class ConversationMatcher
{
    static readonly ILogger<ConversationMatcher> Log = ApplicationLoggerFactory.CreateLogger<ConversationMatcher>();

    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly AISettingsRegistry _aiSettingsRegistry;
    readonly IOpenAIClient _openAIClient;
    readonly ITextAnalyticsClient _textAnalyticsClient;
    readonly FeatureService _featureService;
    readonly IClock _clock;

    /// <summary>
    /// Constructs a <see cref="ConversationMatcher" />.
    /// </summary>
    /// <param name="conversationRepository">A <see cref="IConversationRepository"/>.</param>
    /// <param name="userRepository">Repository used to manage members of an organization.</param>
    /// <param name="aiSettingsRegistry">The registry of AI settings.</param>
    /// <param name="openAIClient">The client used to call Open AI APIs.</param>
    /// <param name="textAnalyticsClient">Client used to redact PII.</param>
    /// <param name="featureService">The <see cref="FeatureService"/> to use to evaluate feature state.</param>
    /// <param name="clock">The clock abstraction.</param>
    public ConversationMatcher(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        AISettingsRegistry aiSettingsRegistry,
        IOpenAIClient openAIClient,
        ITextAnalyticsClient textAnalyticsClient,
        FeatureService featureService,
        IClock clock)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _aiSettingsRegistry = aiSettingsRegistry;
        _openAIClient = openAIClient;
        _textAnalyticsClient = textAnalyticsClient;
        _featureService = featureService;
        _clock = clock;
    }

    /// <summary>
    /// Attempts to select an existing <see cref="Entities.Conversation"/> for this message, first by matching up thread Ids,
    /// then by attempting to use AI (coming soon).
    /// </summary>
    /// <param name="message">The <see cref="ConversationMessage"/> representing the message to select a conversation for.</param>
    /// <returns>A <see cref="Entities.Conversation"/> representing the identified conversation, if any. Otherwise <c>null</c>.</returns>
    public async Task<ConversationMatch> IdentifyConversationAsync(ConversationMessage message)
    {
        if (!message.Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return new ConversationMatch(null, null);
        }

        // Top-level messages have a null `ThreadId`. That's fine, the message ID *is* the ThreadId in this case.
        var threadId = message.ThreadId ?? message.MessageId;

        // Find a conversation by the thread ID
        var conversation = await _conversationRepository.GetConversationByThreadIdAsync(
            threadId,
            message.Room);

        if (conversation is not null
            || message.Organization.Settings.AIEnhancementsEnabled is not true
            || message.IsInThread
            || message.ClassificationResult is null // This means the message is not eligible for AI treatment (aka a skill call).
            || !await _featureService.IsEnabledAsync(FeatureFlags.AIConversationMatching, message.Organization))
        {
            return new ConversationMatch(null, conversation);
        }

        // If we didn't find a conversation by the thread ID, use AI to try and identify a conversation
        // this top-level message belongs to.
        return await MatchConversationUsingAIAsync(message, threadId);
    }

    async Task<ConversationMatch> MatchConversationUsingAIAsync(ConversationMessage message, string threadId)
    {
        var abbot = await _userRepository.EnsureAbbotMemberAsync(message.Organization);

        var conversationMatch = await FindConversationUsingAIAsync(message, abbot);

        if (conversationMatch is { Conversation: { } conversation, Result: not null })
        {
            await _conversationRepository.SaveThreadIdToConversationAsync(conversation, threadId);
        }

        return conversationMatch;
    }

    async Task<ConversationMatch> FindConversationUsingAIAsync(
        ConversationMessage message,
        Member abbot)
    {
        var conversations = await _conversationRepository.GetRecentActiveConversationsAsync(
            message.Room,
            // TODO: Configure this or something.
            TimeSpan.FromDays(14));

        var modelSettings = await _aiSettingsRegistry.GetModelSettingsAsync(AIFeature.ConversationMatcher);
        var systemPrompt = await BuildSystemPrompt(conversations, modelSettings);
        var sanitizedUserMessage = SensitiveDataSanitizer.Sanitize(message.Text, message.SensitiveValues);
        var userPrompt = BuildUserPrompt(sanitizedUserMessage, message);
        int userPromptTokens = userPrompt.TokenCount;
        int systemTokenCount = systemPrompt.TokenCount;
        var totalModelTokens = OpenAIClient.TokenCount(modelSettings.Model);
        var tokensAvailableForPrompt = (totalModelTokens - systemTokenCount - userPromptTokens) * 3 / 4;

        var prompt = BuildMatchConversationPrompt(
                conversations,
                systemPrompt,
                userPrompt,
                tokensAvailableForPrompt)
            .ToList();
        var result = await _openAIClient.GetChatResultAsync(
            prompt,
            modelSettings.Model,
            modelSettings.Temperature,
            abbot);

        var resultText = result.GetResultText();
        var reasonedActions = Reasoned.FromChatResult(result);

        var matchedConversation = reasonedActions is [var action, ..]
                && int.TryParse(action.Action, out var conversationId)
                && conversations.SingleOrDefault(c => c.Id == conversationId) is { } match
            ? match
            : null;

#pragma warning disable CA1508
        if (matchedConversation is null && !message.IsFromSupportee())
#pragma warning restore CA1508
        {
            Log.AgentMessageNotMatched(message.MessageId, resultText);
        }

        var aiResult = new ConversationMatchAIResult
        {
            MessagePrompt = userPrompt.ChatMessage.Format(),
#pragma warning disable CA1508
            CandidateConversationId = matchedConversation?.Id ?? 0,
#pragma warning restore CA1508
            PromptTemplate = modelSettings.Prompt.Text,
            Prompt = prompt.Format(),
            Model = modelSettings.Model,
            ProcessingTime = result.ProcessingTime,
            Directives = Array.Empty<Directive>(),
            ReasonedActions = reasonedActions,
            RawCompletion = resultText ?? string.Empty,
            Temperature = modelSettings.Temperature,
            TokenUsage = TokenUsage.FromUsage(result.Usage),
            UtcTimestamp = _clock.UtcNow,
        };
        return new ConversationMatch(aiResult, matchedConversation);
    }

    string BuildConversationLogEntry(Conversation conversation)
    {
        var participants = string.Join(", ", conversation
            .Members
            .Select(m => SourceUser.FromMemberAndRoom(m.Member, conversation.Room).Format()));

        var lastResponse = (_clock.UtcNow - conversation.LastMessagePostedOn).Humanize();

        return $""""
Conversation {conversation.Id}: """
Participants: {participants}
Tags: {string.Join(", ", conversation.Tags.Select(ct => ct.Tag.Name).Order())}
Last Response: {lastResponse} ago
Summary: {conversation.Summary ?? conversation.Title}"
"""
"""";
    }

    async Task<PromptChatMessage> BuildSystemPrompt(IEnumerable<Conversation> conversations, ModelSettings modelSettings)
    {
        var summaryLog = string.Join("\n\n", conversations.Select(BuildConversationLogEntry));
        var sensitiveValues = await _textAnalyticsClient.RecognizePiiEntitiesAsync(summaryLog);
        var sanitizedLog = SensitiveDataSanitizer.Sanitize(summaryLog, sensitiveValues);
        var template = modelSettings.Prompt.Text;
        return new PromptChatMessage(
            ChatMessageRole.System,
            template.Replace("{Conversation}", sanitizedLog.Message.Reveal(), StringComparison.Ordinal));
    }

    static PromptChatMessage BuildUserPrompt(SanitizedMessage sanitizedMessage, ConversationMessage message)
    {
        var sourceUser = SourceUser.FromMemberAndRoom(message.From, message.Room);
        var tags = message.Categories.Any()
            ? $" (Tags: {string.Join(", ", message.Categories.Select(c => c.ToString()).Order())})"
            : string.Empty;

        return new PromptChatMessage(
            ChatMessageRole.User,
            $"{sourceUser.Format()}{tags}: {sanitizedMessage.Message.Reveal()}");
    }

    static IEnumerable<ChatMessage> BuildMatchConversationPrompt(
        IEnumerable<Conversation> conversations,
        ChatMessage systemPrompt,
        ChatMessage userPrompt,
        int tokensRemaining)
    {
        yield return systemPrompt;

        var matchHistory = EnumerateRecentExamples(conversations);
        // summarizationHistory must be in chronological order and we need to keep it that way.
        // Assume the history is in chronological order. We'll iterate back to find
        int startIndex = GetStartIndex(matchHistory, tokensRemaining);
        foreach (var match in matchHistory.Messages.Skip(startIndex))
        {
            yield return new ChatMessage(ChatMessageRole.User, match.ToPromptText());
            yield return new ChatMessage(ChatMessageRole.Assistant, match.ConversationMatchingInfo!.RawCompletion);
        }

        yield return userPrompt;
    }

    static SanitizedConversationHistory EnumerateRecentExamples(IEnumerable<Conversation> conversations)
    {
        // TODO: We believe that the most recent conversation is the most likely candidate, so I'd like to enumerate
        // all of its examples. Then I'd like to do a breadth first enumeration of the other conversations.
        // But for now, we just enumerate all recent examples and then take as many as we can fit in the prompt.
        var messagePostedEvents = conversations
            .SelectMany(c => c.Events.OfType<MessagePostedEvent>())
            .Select(e => new {
                MessagePostedEvent = e,
                Metadata = JsonSettings.FromJson<MessagePostedMetadata>(e.Metadata)
            })
            // Skip the conversation match events that didn't match a conversation. Since those events lead to
            // creating a Conversation, they would now match a conversation and not be good examples of not
            // matching a conversation.
            .Where(e => e.Metadata is { Text: not null, ConversationMatchAIResult.MessagePrompt.Length: > 0 } and not
            { ConversationMatchAIResult.ReasonedActions: [{ Action: "0" }] })
            .Select(e => e.MessagePostedEvent);

        return SanitizedConversationHistory.Sanitize(messagePostedEvents);
    }

    static int GetStartIndex(SanitizedConversationHistory history, int tokensRemaining)
    {
        for (var i = history.Messages.Count - 1; i >= 0; i--)
        {
            var sourceMessage = history.Messages[i];
            tokensRemaining -= sourceMessage.PromptTokenCount
                               + sourceMessage.ConversationMatchingInfo!.TokenUsage.CompletionTokenCount;
            if (tokensRemaining <= 0)
            {
                // No tokens remaining, meaning we went too far, so back up 1 and then return that index.
                return i + 1;
            }
        }

        return 0;
    }
}

public record ConversationMatch(ConversationMatchAIResult? Result, Conversation? Conversation);

public static partial class ConversationMatcherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Message {MessageId} from Agent not matched to a conversation: {ResultText}")]
    public static partial void AgentMessageNotMatched(
        this ILogger<ConversationMatcher> logger,
        string messageId,
        string? resultText);
}

using System.Collections.Generic;
using System.Linq;
using OpenAI_API.Chat;
using Serious.Cryptography;
using Conversation = Serious.Abbot.Entities.Conversation;

namespace Serious.Abbot.AI;

/// <summary>
/// Represents a set of sanitized <see cref="ChatMessage"/> instance we can pass to the OpenAI chat API.
/// </summary>
/// <param name="Messages">The chat messages.</param>
/// <param name="Replacements">The set of replacements used to restore sensitive values from the completion.</param>
public record SanitizedPromptMessages(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyDictionary<string, SecretString> Replacements)
{
    public static SanitizedPromptMessages BuildSummarizationPromptMessages(
        SanitizedConversationHistory conversationHistory,
        Conversation conversation,
        int tokensRemaining)
    {
        // At this point, we just need to pass these replacements through.
        var replacements = conversationHistory.Replacements;

        var chatMessages = BuildMessages(conversationHistory, conversation, tokensRemaining);
        return new SanitizedPromptMessages(chatMessages.ToList(), replacements);
    }

    static IEnumerable<ChatMessage> BuildMessages(
        SanitizedConversationHistory conversationHistory,
        Conversation conversation,
        int tokensRemaining)
    {
        if (SplitAfterLastSummarized(conversationHistory.Messages) is (var summarized, { Count: > 0 } newMessages))
        {
            var notSummarized = string.Join('\n', newMessages.Select(m => m.ToPromptText()));
            var userPrompt = new PromptChatMessage(ChatMessageRole.User, notSummarized);
            tokensRemaining -= userPrompt.TokenCount;

            var summaryExamplePrompt = summarized.Any()
                    && tokensRemaining > 0
                    && FormatRawSummary(conversation) is { } formatRawSummary
                ? new PromptChatMessage(ChatMessageRole.Assistant, formatRawSummary)
                : null;

            tokensRemaining -= summaryExamplePrompt?.TokenCount ?? 0;

            if (tokensRemaining > 0)
            {
                var historyPrompt = string.Join("\n", BuildHistoryPrompt(summarized, tokensRemaining));

                // Combine all the messages into a single prompt.
                if (historyPrompt.Length > 0)
                {
                    yield return new PromptChatMessage(ChatMessageRole.User, historyPrompt);
                }
            }

            if (summaryExamplePrompt is not null && tokensRemaining > 0)
            {
                // Assistant prompt with the summary.
                yield return summaryExamplePrompt;
            }

            // User prompt.
            yield return userPrompt;
        }
    }

    /// <summary>
    /// Returns each chat message on a new line.
    /// </summary>
    public override string ToString() => string.Join("\n", Messages.Select(FormatChatMessage));

    static string? FormatRawSummary(Conversation conversation)
    {
        return conversation switch
        {
            { Properties: { Summary: { } summary, Conclusion: { } conclusion } } => $"{summary}\n[!conclusion:{conclusion}]",
            { Properties: { Summary: { } summary, Conclusion: null } } => summary,
            { Summary: { } summary } => summary,
            _ => null,
        };
    }

    static string FormatChatMessage(ChatMessage message) => $"{message.Role}: {message.Content}";

    static IEnumerable<string> BuildHistoryPrompt(IReadOnlyList<SourceMessage> sourceMessages, int tokensRemaining)
    {
        // summarizationHistory must be in chronological order and we need to keep it that way.
        // Assume the history is in chronological order. We'll iterate back to find
        int startIndex = GetStartIndex(sourceMessages, tokensRemaining);
        foreach (var message in sourceMessages.Skip(startIndex))
        {
            yield return message.ToPromptText();
        }
    }

    static int GetStartIndex(IReadOnlyList<SourceMessage> history, int tokensRemaining)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var message = history[i];

            var tokens = message.PromptTokenCount;

            tokensRemaining -= tokens;
            if (tokensRemaining <= 0)
            {
                // No tokens remaining, meaning we went too far, so back up 1 and then return that index.
                return i + 1;
            }
        }

        return -1;
    }

    static (IReadOnlyList<SourceMessage>, IReadOnlyList<SourceMessage>) SplitAfterLastSummarized(
        IReadOnlyList<SourceMessage> items)
    {
        int splitIndex = items.GetLastIndexOf(item => item.SummaryInfo is not null) + 1;
        return (items.Take(splitIndex).ToList(), items.Skip(splitIndex).ToList());
    }
}

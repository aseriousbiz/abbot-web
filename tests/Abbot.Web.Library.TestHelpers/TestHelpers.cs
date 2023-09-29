using AI.Dev.OpenAI.GPT;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Services;
using Serious.Cryptography;

public static class TestHelpers
{
    public static string ToCacheKey(this IOrganizationIdentifier organizationIdentifier, string cacheKey)
    {
        return $"{organizationIdentifier.ToCacheKey()}:{cacheKey}";
    }

    public static string ToCacheKey(this IOrganizationIdentifier organizationIdentifier)
    {
        return $"{organizationIdentifier.PlatformId}:{organizationIdentifier.PlatformType}";
    }

    public static MessagePostedMetadata CreateMessagePostedMetadata(
        string message,
        string? summary = null,
        string? conclusion = null,
        IReadOnlyList<SensitiveValue>? sensitiveValues = null,
        ConversationMatchAIResult? conversationMatchAIResult = null)
    {
        var summarizationResult = summary is not null
            ? CreateSummarizationResult(message, summary, conclusion)
            : null;

        return new MessagePostedMetadata
        {
            Categories = Array.Empty<Category>(),
            Text = message,
            SensitiveValues = sensitiveValues ?? Array.Empty<SensitiveValue>(),
            SummarizationResult = summarizationResult,
            ConversationMatchAIResult = conversationMatchAIResult,
        };
    }

    public static SummarizationResult CreateSummarizationResult(
        string message,
        string summary,
        string? conclusion = null)
    {
        var conclusionDirective = conclusion is null
            ? null
            : new Directive("conclusion", conclusion, new List<string>());

        var originalSummary = conclusionDirective is null
            ? summary
            : $"{summary}\n{conclusionDirective}";

        int promptTokenCount = GPT3Tokenizer.Encode(message).Count;
        int summaryTokenCount = GPT3Tokenizer.Encode(originalSummary).Count;

        return new SummarizationResult
        {
            Replacements = new Dictionary<string, SecretString>(),
            Summary = summary,
            RawCompletion = originalSummary,
            PromptTemplate = "prompt",
            Prompt = "prompt",
            Temperature = 1,
            TokenUsage = new TokenUsage(summaryTokenCount, promptTokenCount, promptTokenCount + summaryTokenCount),
            Model = "gpt-4",
            ProcessingTime = default,
            Directives = conclusion is null
                ? Array.Empty<Directive>()
                : new[]
                {
                    new Directive("conclusion",
                        conclusion,
                        new List<string>())
                },
            UtcTimestamp = DateTime.UtcNow,
            ReasonedActions = Array.Empty<Reasoned<string>>(),
        };
    }

    public static string FormatResults(this IEnumerable<SourceMessage> sourceMessages)
        => string.Join("\n", sourceMessages.Select(p => p.ToPromptText() + "\n" + p.SummaryInfo.FormatResults()));

    public static string FormatResults(this CompletionInfo? summaryInfo)
        => summaryInfo is null
            ? string.Empty
            :
            $"""
                - summary: {summaryInfo.RawCompletion}
                - token usage: {summaryInfo.TokenUsage.FormatResults()}
            """;

    public static string FormatResults(this TokenUsage tokenUsage)
        => $"prompt: {tokenUsage.PromptTokenCount}, completion: {tokenUsage.CompletionTokenCount}, total: {tokenUsage.TotalTokenCount}";
}


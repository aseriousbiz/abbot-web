using System.Collections.Generic;
using System.Linq;
using Azure.AI.OpenAI;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Serialization;
using Serious.Cryptography;
using Serious.Text;
using Conversation = Serious.Abbot.Entities.Conversation;

namespace Serious.Abbot.AI;

/// <summary>
/// A client used to summarize text using the OpenAI API.
/// </summary>
/// <remarks>
/// This has support for stripping tokens we use to raise signals.s
/// </remarks>
public class Summarizer
{
    readonly SummarizePromptBuilder _summarizePromptBuilder;
    readonly IOpenAIClient _openAiClient;
    readonly AISettingsRegistry _aiSettings;
    readonly FeatureService _featureService;
    readonly IClock _clock;

    public Summarizer(
        SummarizePromptBuilder summarizePromptBuilder,
        IOpenAIClient openAiClient,
        AISettingsRegistry aiSettings,
        FeatureService featureService,
        IClock clock)
    {
        _summarizePromptBuilder = summarizePromptBuilder;
        _openAiClient = openAiClient;
        _aiSettings = aiSettings;
        _featureService = featureService;
        _clock = clock;
    }

    /// <summary>
    /// Returns a "rolling" summary of a conversation by incorporating the new messages into the existing
    /// summary history for the conversation.
    /// </summary>
    /// <param name="history">The conversation history.</param>
    /// <param name="conversation">The conversation to summarize.</param>
    /// <param name="member">The <see cref="Member"/> that is summarizing the message (aka Abbot).</param>
    /// <param name="organization">The organization where the message occurred in.</param>
    /// <param name="overrideSummarizationSettings">A set of settings to use in place of the stored settings.</param>
    public async Task<SummarizationResult?> SummarizeConversationAsync(
        SanitizedConversationHistory history,
        Conversation conversation,
        Member member,
        Organization organization,
        ModelSettings? overrideSummarizationSettings = null)
    {
        var modelSettings = overrideSummarizationSettings ?? await _aiSettings.GetModelSettingsAsync(AIFeature.Summarization);

        if (organization.Settings.AIEnhancementsEnabled is not true)
        {
            return null;
        }
        if (!await _featureService.IsEnabledAsync(
                FeatureFlags.AIEnhancements,
                organization))
        {
            return null;
        }

        // Henceforth, this feature only supports a GPT model.
        var model = OpenAIClient.IsChatGptModel(modelSettings.Model)
            ? modelSettings.Model
            : "gpt-4";

        var sanitizedPromptMessages = await _summarizePromptBuilder.BuildAsync(
            history,
            conversation,
            overrideSummarizationSettings);

        var chatResult = await _openAiClient.SafelyGetChatResultAsync(
            sanitizedPromptMessages.Messages,
            model,
            modelSettings.Temperature,
            member);
        // If there is more than one choice, the others are just variants of the first one.
        // This would only happen if you pass `n` to the API, which we don't, but we'll be defensive here.
        if (chatResult?.GetResultText() is not { } summary)
        {
            return null;
        }
        summary = SanitizedMessage.Restore(new(summary), sanitizedPromptMessages.Replacements);

        IReadOnlyList<Directive> directives = summary is { Length: > 0 }
            ? DirectivesParser.Parse(summary).ToList()
            : Array.Empty<Directive>();

        var strippedSummary = DirectivesParser.Strip(summary);

        return new SummarizationResult
        {
            Summary = strippedSummary,
            Replacements = sanitizedPromptMessages.Replacements,
            RawCompletion = summary,
            Prompt = sanitizedPromptMessages.ToString(),
            PromptTemplate = modelSettings.Prompt.Text,
            Temperature = modelSettings.Temperature,
            TokenUsage = TokenUsage.FromUsage(chatResult.Usage),
            Model = chatResult.Model,
            ProcessingTime = chatResult.ProcessingTime,
            Directives = directives,
            UtcTimestamp = _clock.UtcNow,
            ReasonedActions = Reasoned.FromChatResult(chatResult),
        };
    }
}

/// <summary>
/// The result of a summarization operation.
/// </summary>
public record SummarizationResult : AIResult
{
    /// <summary>
    /// The most recent sanitized message.
    /// </summary>
    public required IReadOnlyDictionary<string, SecretString>? Replacements { get; init; }

    /// <summary>
    /// The resulting summary, with any <see cref="Directive"/>s removed.
    /// </summary>
    public required string Summary { get; init; }
}

public record ConversationMatchAIResult : AIResult
{
    /// <summary>
    /// The message prompt that was used to match the conversation.
    /// </summary>
    public required string MessagePrompt { get; init; }

    /// <summary>
    /// The Id of the conversation that was matched or <c>0</c> if no conversation was matched.
    /// </summary>
    public required int CandidateConversationId { get; init; }
}

public record TokenUsage(int CompletionTokenCount, int PromptTokenCount, int TotalTokenCount)
{
    public static TokenUsage FromUsage(ChatUsage usage) =>
        new(usage.CompletionTokens, usage.PromptTokens, usage.TotalTokens);

    public static TokenUsage FromUsage(CompletionUsage usage) =>
        new(usage.CompletionTokens, usage.PromptTokens, usage.TotalTokens);

    public static TokenUsage FromUsage(CompletionsUsage usage) =>
        new(usage.CompletionTokens, usage.PromptTokens, usage.TotalTokens);
}

/// <summary>
/// The result of a summarization operation.
/// </summary>
public abstract record AIResult : JsonSettings
{
    /// <summary>
    /// The parsed reasoned actions from the result.
    /// </summary>
    public required IReadOnlyList<Reasoned<string>> ReasonedActions { get; init; } = Array.Empty<Reasoned<string>>();

    /// <summary>
    /// The original summary with any <see cref="Directive"/>s still present.
    /// </summary>
    string? _rawCompletion;

    public required string RawCompletion
    {
        get {
#pragma warning disable CS0618
            return _rawCompletion ??= OriginalSummary ?? string.Empty;
#pragma warning restore CS0618
        }
        init => _rawCompletion = value;
    }

    /// <summary>
    /// The service that produced the result.
    /// </summary>
    public string Service { get; init; } = "OpenAI";

    [Obsolete("Replaced by RawCompletion")]
    public string? OriginalSummary { get; init; }

    /// <summary>
    /// The prompt template used at the time before any replacement strings have been replaced.
    /// </summary>
    public required string PromptTemplate { get; init; }

    /// <summary>
    /// The full prompt used at the time.
    /// </summary>
    public required SecretString Prompt { get; init; }

    /// <summary>
    /// The temperature used for the AI call.
    /// </summary>
    public required double Temperature { get; init; }

    /// <summary>
    /// The token usage for the AI call.
    /// </summary>
    TokenUsage? _tokenUsage;
    public required TokenUsage TokenUsage
    {
        get {
            if (_tokenUsage is null)
            {
#pragma warning disable CS0612
#pragma warning disable CS0618
                _tokenUsage = new TokenUsage(SummaryTokenCount, PromptTokenCount, SummaryTokenCount + PromptTokenCount);
#pragma warning restore CS0618
#pragma warning restore CS0612
            }
            return _tokenUsage;
        }
        init => _tokenUsage = value;
    }

    /// <summary>
    /// The Id of the model used by the AI call.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// The processing time as reported by Chat GPT.
    /// </summary>
    public required TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// The directives found in the result.
    /// </summary>
    public required IReadOnlyList<Directive> Directives { get; init; }

    [Obsolete("We're going to store the token usage in a separate property.")]
    public int PromptTokenCount { get; init; }

    [Obsolete("We're going to store the token usage in a separate property.")]
    public int SummaryTokenCount { get; init; }

    /// <summary>
    /// The date when the AI result was created.
    /// </summary>
    public required DateTime UtcTimestamp { get; init; }
}

/// <summary>
/// Represents a category that a message belongs to.
/// </summary>
/// <param name="Name">Name of the category.</param>
/// <param name="Value">The value of the category.</param>
public record Category(string Name, string Value)
{
    /// <summary>
    /// Outputs the category as a value that can be used as a tag.
    /// </summary>
    public override string ToString() => $"{Name}:{Value}";
}

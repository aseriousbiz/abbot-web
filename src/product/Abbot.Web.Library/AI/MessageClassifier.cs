using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack;
using Serious.Text;

namespace Serious.Abbot.AI;

/// <summary>
/// Service used to classify messages.
/// </summary>
public interface IMessageClassifier
{
    /// <summary>
    /// Returns a set of <see cref="Category"/> instances that describe the message.
    /// </summary>
    /// <param name="message">The message to categorize.</param>
    /// <param name="sensitiveValues">The sensitive values associated with this messaeg.</param>
    /// <param name="messageId">The Slack timestamp of the message.</param>
    /// <param name="room">The room the message is in.</param>
    /// <param name="from">The sender of the message.</param>
    /// <param name="organization">The organization the message was sent to.</param>
    /// <param name="overrideClassifierSettings">The <see cref="ModelSettings"/> to use instead of our stored ones. This is used for testing new settings such as in Staff Tools.</param>
    Task<ClassificationResult?> ClassifyMessageAsync(
        string message,
        IReadOnlyList<SensitiveValue> sensitiveValues,
        string messageId,
        Room room,
        Member from,
        Organization organization,
        ModelSettings? overrideClassifierSettings = null);
}

public class MessageClassifier : IMessageClassifier
{
    static readonly ILogger<MessageClassifier> Log = ApplicationLoggerFactory.CreateLogger<MessageClassifier>();

    readonly IOpenAIClient _openAiClient;
    readonly IAzureOpenAIClient _azureOpenAiClient;
    readonly AISettingsRegistry _aiSettings;
    readonly FeatureService _featureService;
    readonly IClock _clock;

    public MessageClassifier(
        IOpenAIClient openAiClient,
        IAzureOpenAIClient azureOpenAiClient,
        AISettingsRegistry aiSettings,
        FeatureService featureService,
        IClock clock)
    {
        _openAiClient = openAiClient;
        _azureOpenAiClient = azureOpenAiClient;
        _aiSettings = aiSettings;
        _featureService = featureService;
        _clock = clock;
    }

    public async Task<ClassificationResult?> ClassifyMessageAsync(
        string message,
        IReadOnlyList<SensitiveValue> sensitiveValues,
        string messageId,
        Room room,
        Member from,
        Organization organization,
        ModelSettings? overrideClassifierSettings = null)
    {
        if (!SlackTimestamp.TryParse(messageId, out var slackTimestamp))
        {
            return null;
        }

        var sourceUser = SourceUser.FromMemberAndRoom(from, room);
        var sourceMessage = new SourceMessage(message, sourceUser, slackTimestamp);
        var classifierSettings = overrideClassifierSettings ?? await _aiSettings.GetModelSettingsAsync(AIFeature.Classifier);
        var result = await GetCompletionResultAsync(
            classifierSettings,
            sourceMessage,
            sensitiveValues,
            from,
            organization);
        if (result is null)
        {
            return null;
        }

        // Repurpose directives here for classification for now.
        var categories = result.Directives.Select(d => new Category(d.Name, d.RawArguments)).ToList();

        return result with { Categories = categories };
    }

    async Task<ClassificationResult?> GetCompletionResultAsync(
        ModelSettings modelSettings,
        SourceMessage message,
        IEnumerable<SensitiveValue> sensitiveValues,
        Member member,
        Organization organization)
    {
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
        var prompt = await BuildCompletionPromptAsync(modelSettings.Prompt.Text, message, sensitiveValues);

        var (service, client) = _azureOpenAiClient.Enabled
            ? ("Azure OpenAI", _azureOpenAiClient)
            : ("OpenAI", _openAiClient);

        var completion = await client.SafelyGetCompletionAsync(
            prompt.Message.Reveal(),
            modelSettings.Model,
            (float)modelSettings.Temperature,
            member);

        if (completion is null && _azureOpenAiClient.Enabled)
        {
            // When we deploy our Azure OpenAI, it can take a moment for the Model Deployment to be available.
            // In that case, we want to fallback to OpenAI. This is only a problem in the beginning.
            // So we can remove this code later once everything is deployed.
            service = "OpenAI";
            completion = await _openAiClient.SafelyGetCompletionAsync(
                prompt.Message.Reveal(),
                modelSettings.Model,
                (float)modelSettings.Temperature,
                member);
        }

        // If there is more than one completion, the others are just variants of the first one.
        // This would only happen if you pass `n` to the API, which we don't, but we'll be defensive here.
        if (completion?.GetResultText() is not { Length: > 0 } summary)
        {
            return null;
        }

        IReadOnlyList<Directive> directives = summary is { Length: > 0 }
            ? DirectivesParser.Parse(summary).ToList()
            : Array.Empty<Directive>();
        if (directives.Any())
        {
            Log.DirectivesFoundInCompletion(summary);
        }

        return new ClassificationResult
        {
            Service = service,
            RawCompletion = summary,
            Prompt = prompt.Message,
            PromptTemplate = modelSettings.Prompt.Text,
            Temperature = modelSettings.Temperature,
            TokenUsage = TokenUsage.FromUsage(completion.Usage),
            Model = completion.Model,
            ProcessingTime = completion.ProcessingTime,
            Directives = directives,
            UtcTimestamp = _clock.UtcNow,
            ReasonedActions = Reasoned.FromChatResult(completion),
        };
    }

    async Task<SanitizedMessage> BuildCompletionPromptAsync(
        string promptTemplate,
        SourceMessage message,
        IEnumerable<SensitiveValue> sensitiveValues)
    {
        var sanitizedMessage = _azureOpenAiClient.Enabled
            // We don't need to sanitize messages sent to Azure.
            ? new SanitizedMessage(message.Text, new Dictionary<string, SecretString>())
            : SensitiveDataSanitizer.Sanitize(message.Text, sensitiveValues);

        var prompt = promptTemplate.Replace("{Conversation}", sanitizedMessage.Message.Reveal(), StringComparison.Ordinal);

        return sanitizedMessage with { Message = prompt };
    }
}

/// <summary>
/// The result of a classification operation.
/// </summary>
public record ClassificationResult : AIResult
{
    public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
}

static partial class MessageClassifierLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Directives found in summary: {Summary}")]
    public static partial void DirectivesFoundInCompletion(
        this ILogger<MessageClassifier> logger, string summary);
}

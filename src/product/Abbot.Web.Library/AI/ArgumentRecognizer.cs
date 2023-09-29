using System.Collections.Generic;
using System.Linq;
using Azure.AI.TextAnalytics;
using OpenAI_API.Chat;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.Text;

namespace Serious.Abbot.AI;

public class ArgumentRecognizer
{
    readonly IOpenAIClient _openAIClient;
    readonly ITextAnalyticsClient _textAnalyticsClient;
    readonly AISettingsRegistry _aiSettings;
    readonly IClock _clock;

    // We don't want to redact certain categories of PII in arguments since:
    // 1. The user _explicitly_ called the skill, which _explicitly_ enabled argument recognition
    // 2. The AI may have been trained to understand some of the data in these categories, so just tokenizing them won't work.
    static readonly HashSet<PiiEntityCategory> UnredactedCategoriesInArguments = new()
    {
        PiiEntityCategory.Address,
        PiiEntityCategory.Age,
        PiiEntityCategory.Date,
        new("DateTime"),
        PiiEntityCategory.Email,
        PiiEntityCategory.Organization,
        PiiEntityCategory.Person,
        new("PersonType"),
        PiiEntityCategory.PhoneNumber,
        PiiEntityCategory.IPAddress,
        PiiEntityCategory.URL,
        PiiEntityCategory.Default,

        // Importantly, we keep all the highly-sensitive categories (Credit Card Numbers, SSNs, etc.) redacted.
    };

    public ArgumentRecognizer(
        IOpenAIClient openAIClient,
        ITextAnalyticsClient textAnalyticsClient,
        AISettingsRegistry aiSettings,
        IClock clock)
    {
        _openAIClient = openAIClient;
        _textAnalyticsClient = textAnalyticsClient;
        _aiSettings = aiSettings;
        _clock = clock;
    }

    public async Task<ArgumentRecognitionResult> RecognizeArgumentsAsync(
        Skill skill,
        IEnumerable<SkillExemplar> exemplars,
        string message,
        Member actor)
    {
        // Sanitize the message
        var piiResult = await _textAnalyticsClient.RecognizePiiEntitiesAsync(message);
        var sanitizedMessage = SensitiveDataSanitizer.Sanitize(message, piiResult, UnredactedCategoriesInArguments);

        var modelSettings = await _aiSettings.GetModelSettingsAsync(AIFeature.ArgumentRecognizer);

        var (result, prompt) = await GetDirectivesFromChatApiAsync(
                skill,
                exemplars,
                sanitizedMessage,
                modelSettings,
                actor);
        var completion = result.GetResultText() ?? string.Empty;
        var directives = DirectivesParser.Parse(completion).ToList();
        var arguments = directives.Any(d => d.Name == "null")
            // The "null" directive indicates that the model was unable to recognize any arguments.
            // We use this directive so that we're resilient to the model injecting random opinions and hallucinations :)
            ? string.Empty
            // Ok, no `null` directive, so the remaining text are the arguments. Trim leading and trailing spaces, unredact and go!
            : SanitizedMessage.Restore(ExtractArguments(completion.Trim()), sanitizedMessage.Replacements);

        return new ArgumentRecognitionResult
        {
            Arguments = arguments,
            ProcessingTime = result.ProcessingTime,
            Prompt = prompt,
            Temperature = modelSettings.Temperature,
            TokenUsage = TokenUsage.FromUsage(result.Usage),
            Model = modelSettings.Model,
            RawCompletion = completion,
            PromptTemplate = modelSettings.Prompt.Text,
            Directives = directives,
            UtcTimestamp = _clock.UtcNow,
            ReasonedActions = Reasoned.FromChatResult(result),
        };
    }

    async Task<(ChatResult, string)> GetDirectivesFromChatApiAsync(
        Skill skill,
        IEnumerable<SkillExemplar> exemplars,
        SanitizedMessage sanitizedMessage,
        ModelSettings modelSettings,
        Member actor)
    {
        var promptMessages = GenerateChatGptPrompt(skill, exemplars, sanitizedMessage.Message.Reveal(), modelSettings).ToList();

        var result = await _openAIClient.GetChatResultAsync(
            promptMessages,
            modelSettings.Model,
            modelSettings.Temperature,
            actor);

        var promptText = promptMessages.Format();
        return (result, promptText);
    }

    static string ExtractArguments(string input)
    {
        if (input.StartsWith("\"\"\"\n", StringComparison.Ordinal) && input.EndsWith("\n\"\"\"", StringComparison.Ordinal))
        {
            return input[4..^4];
        }

        return input;
    }

    static IEnumerable<ChatMessage> GenerateChatGptPrompt(
        Skill skill,
        IEnumerable<SkillExemplar> exemplars,
        string message,
        ModelSettings modelSettings)
    {
        var flattery = modelSettings.Prompt.Text.Replace("{SkillName}", skill.Name, StringComparison.Ordinal);

        yield return new ChatMessage(ChatMessageRole.System, flattery);

        foreach (var exemplar in exemplars)
        {
            if (exemplar.Properties.Arguments is not { Length: > 0 } expectedArguments)
            {
                continue;
            }

            yield return new ChatMessage(ChatMessageRole.User, exemplar.Exemplar);
            yield return new ChatMessage(ChatMessageRole.Assistant, expectedArguments);
        }

        yield return new ChatMessage(ChatMessageRole.User, message);
    }
}

public record ArgumentRecognitionResult : AIResult
{
    /// <summary>
    /// The resulting arguments.
    /// </summary>
    public required string Arguments { get; init; }
}

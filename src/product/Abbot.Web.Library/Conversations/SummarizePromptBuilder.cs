using System.Linq;
using AI.Dev.OpenAI.GPT;
using OpenAI_API.Chat;
using Serious.Abbot.AI.Templating;
using Conversation = Serious.Abbot.Entities.Conversation;

namespace Serious.Abbot.AI;

/// <summary>
/// Encapsulates the strategy we use for building up a summarize prompt.
/// </summary>
public class SummarizePromptBuilder
{
    readonly PromptCompiler _promptCompiler;
    readonly AISettingsRegistry _aiSettings;

    public SummarizePromptBuilder(PromptCompiler promptCompiler, AISettingsRegistry aiSettings)
    {
        _promptCompiler = promptCompiler;
        _aiSettings = aiSettings;
    }

    public async Task<SanitizedPromptMessages> BuildAsync(
        SanitizedConversationHistory history,
        Conversation conversation,
        ModelSettings? overrideSummarizationSettings = null)
    {
        var modelSettings = overrideSummarizationSettings
            ?? await _aiSettings.GetModelSettingsAsync(AIFeature.Summarization);

        var compiledPrompt = _promptCompiler.Compile(modelSettings.Prompt.Text);
        var context = new {
            Conversation = conversation,
        };
        var systemPrompt = compiledPrompt(context);
        var systemTokenCount = GPT3Tokenizer.Encode(systemPrompt).Count;
        var totalModelTokens = OpenAIClient.TokenCount(modelSettings.Model);

        // We're going to reserve 1/4 of the tokens for the assistant's response.
        var tokensAvailableForPrompt = (totalModelTokens - systemTokenCount) * 3 / 4;

        var prompts = SanitizedPromptMessages.BuildSummarizationPromptMessages(
            history,
            conversation,
            tokensAvailableForPrompt);

        var systemPromptMessage = new[] { new ChatMessage(ChatMessageRole.System, systemPrompt) };
        return new(systemPromptMessage.Concat(prompts.Messages).ToList(), prompts.Replacements);
    }
}

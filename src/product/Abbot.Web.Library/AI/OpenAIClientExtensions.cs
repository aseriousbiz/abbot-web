using System.Collections.Generic;
using System.Linq;
using Azure.AI.OpenAI;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using ChatMessage = OpenAI_API.Chat.ChatMessage;
using Choice = OpenAI_API.Completions.Choice;

namespace Serious.Abbot.AI;

public static class OpenAIClientExtensions
{
    /// <summary>
    /// Returns the <see cref="OpenAI_API.Chat.ChatChoice.Message"/> of the first <see cref="OpenAI_API.Chat.ChatChoice"/> in the
    /// <see cref="ChatResult"/>
    /// </summary>
    /// <param name="result">The <see cref="ChatResult"/>.</param>
    public static string? GetResultText(this ChatResult result)
    {
        return result.Choices is [var firstChoice, ..]
            ? firstChoice.Message.Content
            : null;
    }

    /// <summary>
    /// Returns the <see cref="OpenAI_API.Chat.ChatChoice.Message"/> of the first <see cref="OpenAI_API.Chat.ChatChoice"/> in the
    /// <see cref="ChatResult"/>
    /// </summary>
    /// <param name="result">The <see cref="ChatResult"/>.</param>
    public static string? GetResultText(this Completions result)
    {
        return result.Choices is [var firstChoice, ..]
            ? firstChoice.Text
            : null;
    }

    /// <summary>
    /// Returns the <see cref="OpenAI_API.Completions.Choice.Text"/> of the first <see cref="OpenAI_API.Completions.Choice"/> in the
    /// <see cref="CompletionResult"/>
    /// </summary>
    /// <param name="result">The <see cref="CompletionResult"/>.</param>
    public static string? GetResultText(this CompletionResult result)
    {
        return result.Completions is [var firstCompletion, ..]
            ? firstCompletion.Text
            : null;
    }

    /// <summary>
    /// Converts an Azure OpenAI <see cref="Completions"/> into a <see cref="CompletionResult"/>.
    /// </summary>
    /// <param name="completions">The <see cref="Completions"/> returned from AzureAI.</param>
    /// <returns></returns>
    public static CompletionResult ToCompletionResult(this Completions completions)
    {
        return new CompletionResult
        {
            Completions = completions.Choices.Select(c => new Choice
            {
                Text = c.Text,
                Index = c.Index.GetValueOrDefault(),
            }).ToList(),
            Usage = new CompletionUsage
            {
                PromptTokens = completions.Usage.PromptTokens,
                CompletionTokens = (short)completions.Usage.CompletionTokens,
                TotalTokens = completions.Usage.TotalTokens,
            },
            Model = completions.Model,
            Id = completions.Id,
            CreatedUnixTime = completions.Created,
        };
    }

    public static string Format(this IEnumerable<ChatMessage> chatMessages)
        => string.Join("\n", chatMessages.Select(p => $"{Format(p)}\n"));

    public static string Format(this ChatMessage chatMessage) => $"{chatMessage.Role}: {chatMessage.Content}";
}

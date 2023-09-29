using Serious.Abbot.Entities;

namespace Serious.Abbot.AI;

/// <summary>
/// Represents completion information associated with a message and an AI operation. For example, when summarizing a
/// message, this would contain information about the summary completion. When matching a message to a conversation,
/// this would match the completion to the message.
/// </summary>
/// <param name="RawCompletion">The raw completion returned from the Open AI API.</param>
/// <param name="TokenUsage">The token usage returned.</param>
public record CompletionInfo(string RawCompletion, TokenUsage TokenUsage)
{
    /// <summary>
    /// Creates a <see cref="CompletionInfo"/> for a summarization operation.
    /// </summary>
    /// <param name="aiResult">The summarization result.</param>
    /// <returns></returns>
    public static CompletionInfo? FromAIResult(AIResult? aiResult)
        => aiResult is not null
            ? new CompletionInfo(aiResult.RawCompletion, aiResult.TokenUsage)
            : null;
}

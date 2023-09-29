using AI.Dev.OpenAI.GPT;
using Serious.Cryptography;
using Serious.Slack;

namespace Serious.Abbot.AI;

/// <summary>
/// A source message that will be summarized.
/// </summary>
public record SourceMessage
{
    public static readonly SourceMessage Empty = new(
        SecretString.EmptySecret,
        null,
        SlackTimestamp.Parse("0000000000.000000"));

    readonly string _promptText;

    public SourceMessage(
        SecretString text,
        SourceUser? user,
        SlackTimestamp timestamp,
        CompletionInfo? summaryInfo = null,
        CompletionInfo? conversationMatchingInfo = null)
    {
        var roleString = user is not null
            ? $" ({user.Role})"
            : string.Empty;
        _promptText = $"<@{user?.Id}>{roleString} says: {text.Reveal()}";
        Text = text;
        User = user;
        // If we summarized or conversation matched this message, then we'll use the prompt token count as reported by the API.
        PromptTokenCount = summaryInfo?.TokenUsage.PromptTokenCount
                ?? conversationMatchingInfo?.TokenUsage.PromptTokenCount
                ?? GPT3Tokenizer.Encode(_promptText).Count;
        Timestamp = timestamp;
        SummaryInfo = summaryInfo;
        ConversationMatchingInfo = conversationMatchingInfo;
    }

    /// <summary>
    /// The text of the message.
    /// </summary>
    public SecretString Text { get; }

    /// <summary>
    /// The user that sent the message.
    /// </summary>
    public SourceUser? User { get; }

    /// <summary>
    /// The number of tokens for the prompt text.
    /// </summary>
    public int PromptTokenCount { get; }

    /// <summary>
    /// If this message was summarized, this has information about the summary.
    /// </summary>
    public CompletionInfo? SummaryInfo { get; }

    /// <summary>
    /// If Abbot tried to match this message to a conversation, this has information about the result.
    /// </summary>
    public CompletionInfo? ConversationMatchingInfo { get; }

    /// <summary>
    /// The Slack timestamp of the message.
    /// </summary>
    public SlackTimestamp Timestamp { get; init; }

    /// <summary>
    /// Returns the message in a form to be included in a prompt.
    /// </summary>
    /// <returns>The text to use in a prompt.</returns>
    public string ToPromptText() => _promptText;
}

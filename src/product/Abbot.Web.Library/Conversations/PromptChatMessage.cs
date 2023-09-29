using AI.Dev.OpenAI.GPT;
using OpenAI_API.Chat;

namespace Serious.Abbot.AI;

/// <summary>
/// A <see cref="ChatMessage"/> with a calculated token count. This type has an implicit conversion to
/// <see cref="ChatMessage"/>.
/// </summary>
/// <param name="ChatMessage">The <see cref="ChatMessage"/>.</param>
/// <param name="TokenCount">The token count.</param>
public record PromptChatMessage(ChatMessage ChatMessage, int TokenCount)
{
    public PromptChatMessage(ChatMessageRole role, string content)
        : this(new ChatMessage(role, content), GPT3Tokenizer.Encode(content).Count)
    {
    }

    public static implicit operator ChatMessage(PromptChatMessage message) => message.ChatMessage;
}

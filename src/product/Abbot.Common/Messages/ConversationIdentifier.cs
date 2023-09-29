using System.Diagnostics;
using System.Globalization;

namespace Serious.Abbot.Messages;

/// <summary>
/// Encapsulates information that can be used to identify a Conversation. Either the channel + message OR the
/// conversation's database Id.
/// </summary>
/// <param name="Channel">The channel of the message.</param>
/// <param name="MessageId">The message Id.</param>
/// <param name="ConversationId">The database Id of the Conversation.</param>
public record ConversationIdentifier(string? Channel, string? MessageId, int? ConversationId = null)
{
    public override string ToString() => ConversationId.HasValue
        ? $"{ConversationId.Value}"
        : $"{Channel}:{MessageId}";

    public static implicit operator string(ConversationIdentifier identifier) => identifier.ToString();

    public static ConversationIdentifier Parse(string value)
    {
        return value.Split(':', 2) switch
        {
            [var channel, var messageId] => new ConversationIdentifier(channel, messageId),
            [var conversationId] => new ConversationIdentifier(
                Channel: null,
                MessageId: null,
                ConversationId: int.Parse(conversationId, CultureInfo.InvariantCulture)),
            _ => throw new UnreachableException()
        };
    }
}

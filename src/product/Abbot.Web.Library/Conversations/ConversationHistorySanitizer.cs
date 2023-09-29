using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Cryptography;

namespace Serious.Abbot.AI;

/// <summary>
/// Represents a sanitized conversation history we can send to any AI services.
/// </summary>
public record SanitizedConversationHistory(
    IReadOnlyList<SourceMessage> Messages,
    IReadOnlyDictionary<string, SecretString> Replacements)
{
    /// <summary>
    /// Given a set of <see cref="MessagePostedEvent"/> instances for a conversation, returns a sanitized version of the
    /// history.
    /// </summary>
    /// <param name="messages">The set of messages.</param>
    public static SanitizedConversationHistory Sanitize(IEnumerable<MessagePostedEvent> messages)
        => Sanitize(BuildHistory(messages).ToList());

    static SanitizedConversationHistory Sanitize(IReadOnlyList<SanitizedSourceMessage> sanitizedSourceMessages)
    {
        var replacements = sanitizedSourceMessages is [.., var lastSourceMessage]
            ? lastSourceMessage.Replacements
            : new Dictionary<string, SecretString>();
        return new SanitizedConversationHistory(
            sanitizedSourceMessages.Select(m => m.SourceMessage).ToList(),
            replacements);
    }

    static IEnumerable<SanitizedSourceMessage> BuildHistory(IEnumerable<MessagePostedEvent> messages)
    {
        IReadOnlyDictionary<string, SecretString> replacements = new Dictionary<string, SecretString>();
        foreach (var message in messages)
        {
            if (SanitizedSourceMessage.FromMessagePostedEvent(message, replacements) is { } sanitizedSourceMessage)
            {
                replacements = sanitizedSourceMessage.Replacements;
                yield return sanitizedSourceMessage;
            }
        }
    }
}

using Serious.Slack.InteractiveMessages;

namespace Serious.Slack;

/// <summary>
/// Extensions on <see cref="SlackMessage"/>.
/// </summary>
public static class MessageExtensions
{
    /// <summary>
    /// Returns <c>true</c> if this message is a reply in a thread and not a top-level message.
    /// See ConversationMember.IsInThread for another example.
    /// </summary>
    /// <param name="message">A message.</param>
    public static bool IsInThread(this SlackMessage message)
    {
        return message.ThreadTimestamp is { Length: > 0 } && message.ThreadTimestamp != message.Timestamp;
    }

    /// <summary>
    /// Returns <c>true</c> if this message was deleted in Slack.
    /// </summary>
    /// <param name="message">The message.</param>
    public static bool IsDeleted(this SlackMessage message) => message.SubType is "tombstone";
}

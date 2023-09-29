namespace Serious.Abbot.Eventing.Messages;

public class PublishSystemNotification
{
    /// <summary>
    /// The content of the message to publish to the system notification channel
    /// </summary>
    public required MessageContent Content { get; init; }

    /// <summary>
    /// A simple string that can be used to ensure only a single version of this notification is published.
    /// This isn't a guarantee, but it should generally work if a series of messages with the same key are published
    /// in quick succession.
    /// </summary>
    public string? DeduplicationKey { get; init; }
}

using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public class ConversationStateChanged
{
    public required ConversationIds Conversation { get; init; }

    public required MemberIds Actor { get; init; }

    /// <inheritdoc cref="StateChangedEvent.OldState"/>
    public ConversationState OldState { get; init; }

    /// <inheritdoc cref="StateChangedEvent.NewState"/>
    public ConversationState NewState { get; init; }

    /// <inheritdoc cref="StateChangedEvent.Implicit"/>
    public bool Implicit { get; init; }

    /// <inheritdoc cref="ConversationEvent.Created"/>
    public required DateTime Timestamp { get; init; }

    /// <inheritdoc cref="ConversationEvent.MessageId"/>
    public required string? MessageId { get; init; }

    /// <inheritdoc cref="ConversationEvent.ThreadId"/>
    public required string? ThreadId { get; init; }

    /// <inheritdoc cref="ConversationEvent.MessageUrl"/>
    public required Uri? MessageUrl { get; init; }
}

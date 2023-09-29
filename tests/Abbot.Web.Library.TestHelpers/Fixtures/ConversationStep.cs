namespace Abbot.Common.TestHelpers;

// Represents a step in a conversation.
public record ConversationStep(ConversationAction ConversationAction, int DaysAgo);

public enum ConversationAction
{
    FirstResponderResponds,
    CustomerResponds,
    ConversationClosed,
    ConversationOverdue,
    ConversationSnoozed,
    ConversationAwakened,
}

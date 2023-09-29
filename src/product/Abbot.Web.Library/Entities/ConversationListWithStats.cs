using Serious.Collections;

namespace Serious.Abbot.Entities;

public record ConversationListWithStats(IPaginatedList<Conversation> Conversations, ConversationStats Stats);

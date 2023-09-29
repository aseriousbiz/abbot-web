using MassTransit;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Eventing;

/// <summary>
/// Marks a message type as using the <see cref="ConversationId"/> field as the Session ID.
/// </summary>
[ExcludeFromTopology]
public interface ISessionFromConversation
{
    Id<Conversation> ConversationId { get; }
}

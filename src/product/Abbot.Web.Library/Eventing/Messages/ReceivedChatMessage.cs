using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

/// <summary>
/// Message triggered when Abbot receives a chat message.
/// </summary>
public record ReceivedChatMessage : IOrganizationMessage
{
    /// <summary>
    /// A <see cref="Messages.ChatMessage"/> describing the actual content and metadata about the received message,
    /// as provided by the chat platform.
    /// </summary>
    public required ChatMessage ChatMessage { get; init; }

    public Id<Organization> OrganizationId => ChatMessage.Event.OrganizationId;
}

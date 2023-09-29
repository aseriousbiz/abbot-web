using Serious.Abbot.Entities;

namespace Serious.Abbot.Eventing.Entities;

public record ConversationIds
{
    public required Id<Conversation> Id { get; init; }
    public required Id<Organization> OrganizationId { get; init; }
    public required Id<Room> RoomId { get; init; }
    public required Id<Hub>? HubId { get; init; }

    public static implicit operator ConversationIds(Conversation conversation) => new()
    {
        Id = conversation,
        OrganizationId = (Id<Organization>)conversation.OrganizationId,
        RoomId = (Id<Room>)conversation.RoomId,
        HubId = (Id<Hub>?)conversation.HubId,
    };
}

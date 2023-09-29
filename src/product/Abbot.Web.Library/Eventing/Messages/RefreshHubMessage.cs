using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public class RefreshHubMessage : IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
    public required Id<Conversation> ConversationId { get; init; }
}

using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public record CreateDefaultHub : IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
    public required Id<Member> ActorId { get; init; }
    public required string Name { get; init; }
}

using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public record OrganizationActivated : IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
}

public record OrganizationUpdated : IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
}

public record ResyncOrganizationCustomer : IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
}

using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations;

public interface IUserIdentityLinker
{
    LinkedIdentityType Type { get; }

    Task<IntegrationLink?> ResolveIdentityAsync(Organization organization, Member member);
}

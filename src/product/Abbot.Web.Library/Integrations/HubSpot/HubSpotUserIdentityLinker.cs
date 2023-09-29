using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Integrations.HubSpot;

public class HubSpotUserIdentityLinker : IUserIdentityLinker
{
    readonly IIntegrationRepository _integrationRepository;
    readonly IHubSpotClientFactory _hubSpotClientFactory;
    readonly IHubSpotResolver _hubSpotResolver;

    public HubSpotUserIdentityLinker(
        IIntegrationRepository integrationRepository,
        IHubSpotClientFactory hubSpotClientFactory,
        IHubSpotResolver hubSpotResolver)
    {
        _integrationRepository = integrationRepository;
        _hubSpotClientFactory = hubSpotClientFactory;
        _hubSpotResolver = hubSpotResolver;
    }

    public LinkedIdentityType Type => LinkedIdentityType.HubSpot;

    public async Task<IntegrationLink?> ResolveIdentityAsync(Organization organization, Member member)
    {
        var (integration, settings) = await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(organization);
        if (integration?.Enabled is not true || settings is null)
        {
            return null;
        }

        var client = await _hubSpotClientFactory.CreateClientAsync(integration, settings);
        return await _hubSpotResolver.ResolveHubSpotContactAsync(client, integration, member);
    }
}

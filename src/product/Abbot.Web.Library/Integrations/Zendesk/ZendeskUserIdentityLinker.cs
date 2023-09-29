using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Integrations.Zendesk;

public class ZendeskUserIdentityLinker : IUserIdentityLinker
{
    readonly IIntegrationRepository _integrationRepository;
    readonly IZendeskClientFactory _zendeskClientFactory;
    readonly IZendeskResolver _zendeskResolver;

    public ZendeskUserIdentityLinker(
        IIntegrationRepository integrationRepository,
        IZendeskClientFactory zendeskClientFactory,
        IZendeskResolver zendeskResolver)
    {
        _integrationRepository = integrationRepository;
        _zendeskClientFactory = zendeskClientFactory;
        _zendeskResolver = zendeskResolver;
    }

    public LinkedIdentityType Type => LinkedIdentityType.Zendesk;

    public async Task<IntegrationLink?> ResolveIdentityAsync(Organization organization, Member member)
    {
        var (integration, settings) = await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);
        if (integration?.Enabled is not true || settings is null)
        {
            return null;
        }

        var client = _zendeskClientFactory.CreateClient(settings);
        var user = await _zendeskResolver.ResolveZendeskIdentityAsync(client, organization, member, null);
        return ZendeskUserLink.Parse(user?.Url);
    }
}

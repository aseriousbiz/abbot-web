using System.Globalization;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.HubSpot;

public abstract class HubSpotPageBase : SingleIntegrationPageBase<HubSpotSettings>
{
    public IOptions<HubSpotOptions> HubSpotOptions { get; }

    protected HubSpotPageBase(IIntegrationRepository integrationRepository, IOptions<HubSpotOptions> hubSpotOptions)
        : base(integrationRepository)
    {
        HubSpotOptions = hubSpotOptions;
    }

    public bool IsInstalled => Settings is { AccessToken: not null };

    public bool IsConfigured => Settings is { HasTicketConfig: true };

    public long? PortalId => Integration.ExternalId is { } externalId
        ? long.Parse(externalId, CultureInfo.InvariantCulture)
        : null;

    public string? HubSpotDomain => Settings.HubDomain;
}

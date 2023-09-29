using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.HubSpot;

public class IndexPage : HubSpotPageBase
{
    public IndexPage(IIntegrationRepository integrationRepository,
        IOptions<HubSpotOptions> hubSpotOptions) : base(integrationRepository, hubSpotOptions)
    {
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostUninstallAsync()
    {
        Settings.AccessToken = null;
        Settings.RefreshToken = null;
        Settings.RedirectUri = null;
        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);

        // Disable the integration too, it can't be enabled and uninstalled
        await IntegrationRepository.DisableAsync(Organization, IntegrationType.HubSpot, Viewer);

        return RedirectWithStatusMessage("The HubSpot integration has been uninstalled.");
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        if (Integration is { Enabled: true })
        {
            await IntegrationRepository.DisableAsync(Organization, IntegrationType.HubSpot, Viewer);
        }

        return RedirectWithStatusMessage("The HubSpot integration has been disabled.");
    }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        if (Integration is not { Enabled: true })
        {
            await IntegrationRepository.EnableAsync(Organization, IntegrationType.HubSpot, Viewer);
        }

        // It was already enabled, just stay on this page.
        // This means someone clicked the button twice, or synthesized a POST.
        return RedirectWithStatusMessage("The HubSpot integration has been enabled.");
    }
}

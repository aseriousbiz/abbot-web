using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.SlackApp;

public class IndexModel : SlackAppPageBase
{
    readonly IOrganizationRepository _organizationRepository;
    readonly ISlackIntegration _slackIntegration;
    readonly ISlackAuthenticator _slackAuthenticator;

    public IndexModel(IIntegrationRepository integrationRepository,
        IOrganizationRepository organizationRepository,
        ISlackIntegration slackIntegration,
        ISlackAuthenticator slackAuthenticator)
        : base(integrationRepository)
    {
        _organizationRepository = organizationRepository;
        _slackIntegration = slackIntegration;
        _slackAuthenticator = slackAuthenticator;
    }

    public bool HasRooms { get; private set; }

    public async Task OnGetAsync()
    {
        HasRooms = await _slackIntegration.HasRoomMembershipAsync(Settings.Authorization);
    }

    public async Task<IActionResult> OnPostInstallAsync()
    {
        if (!CanInstall)
        {
            return RedirectWithStatusMessage("The Custom Slack App is not configured.");
        }

        var action = Security.OAuthAction.InstallCustom;
        var correlationValue = _slackAuthenticator.GetStateAndSetCorrelationCookie(HttpContext, Organization.Id, action);
        var installUrl = await _slackAuthenticator.GetInstallUrlAsync(Organization, action, correlationValue);

        // Let's GOOOOO
        return Redirect(installUrl);
    }

    public async Task<IActionResult> OnPostUninstallAsync()
    {
        if (IsEnabled)
        {
            return RedirectWithStatusMessage("Cannot uninstall while Enabled.");
        }

        await _slackIntegration.UninstallAsync(Organization, Viewer);

        return RedirectWithStatusMessage("The Custom Slack App has been uninstalled.");
    }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        if (IsEnabled)
        {
            // Nothing to do
            return RedirectToPage();
        }

        await _slackIntegration.EnableAsync(Organization, Viewer);

        return RedirectWithStatusMessage("The Custom Slack App has been enabled.");
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        if (!IsEnabled)
        {
            // Nothing to do
            return RedirectToPage();
        }

        await _slackIntegration.DisableAsync(Organization, Viewer);

        return RedirectWithStatusMessage("The Custom Slack App has been disabled.");
    }
}

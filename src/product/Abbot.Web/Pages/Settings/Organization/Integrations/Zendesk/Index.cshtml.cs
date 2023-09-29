using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.Zendesk;

public class IndexPage : ZendeskPageBase
{
    static readonly ILogger<IndexPage> Log = ApplicationLoggerFactory.CreateLogger<IndexPage>();

    readonly IZendeskInstaller _zendeskInstaller;

    public IndexPage(IIntegrationRepository integrationRepository,
        IZendeskInstaller zendeskInstaller, IOptions<ZendeskOptions> zendeskOptions) : base(integrationRepository, zendeskOptions)
    {
        _zendeskInstaller = zendeskInstaller;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostUninstallAsync()
    {
        if (Integration is not { Enabled: true })
        {
            StatusMessage = "Zendesk integration is not enabled";
            return RedirectToPage();
        }

        if (Settings.ApiToken is null)
        {
            StatusMessage = "No Zendesk credentials configured";
            return RedirectToPage();
        }

        try
        {
            await _zendeskInstaller.UninstallFromZendeskAsync(Organization, Settings);
        }
        catch (Exception ex)
        {
            Log.ZendeskUninstallationFailed(ex, Settings.GetTokenPrefix().Require());
            StatusMessage =
                $"Failed to uninstall Zendesk integration. Please try again, or contact '{WebConstants.SupportEmail}' for help.";

            return RedirectToPage();
        }

        // Save changes to settings
        await IntegrationRepository.SaveSettingsAsync(Integration!, Settings);

        // Disable the integration, if it isn't already disabled
        await IntegrationRepository.DisableAsync(Organization, IntegrationType.Zendesk, Viewer);

        StatusMessage = "Zendesk integration uninstalled";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        if (Integration is { Enabled: true })
        {
            await IntegrationRepository.DisableAsync(Organization, IntegrationType.Zendesk, Viewer);
        }

        return RedirectWithStatusMessage("The Zendesk integration has been disabled.");
    }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        if (Integration is not { Enabled: true })
        {
            await IntegrationRepository.EnableAsync(Organization, IntegrationType.Zendesk, Viewer);
        }

        return RedirectWithStatusMessage("The Zendesk integration has been enabled.");
    }
}

static partial class ZendeskPageLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level =
            LogLevel.Information, // This is not an _error_, it's user error, but we log it so support can look up the details.
        Message = "Failed to install Zendesk integration using token prefix: {TokenPrefix}.")]
    public static partial void ZendeskInstallationFailed(this ILogger<IndexPage> logger, Exception ex,
        string tokenPrefix);

    [LoggerMessage(
        EventId = 2,
        Level =
            LogLevel.Information, // This is not an _error_, it's user error, but we log it so support can look up the details.
        Message = "Failed to uninstall Zendesk integration using token prefix: {TokenPrefix}.")]
    public static partial void ZendeskUninstallationFailed(this ILogger<IndexPage> logger, Exception ex,
        string tokenPrefix);
}

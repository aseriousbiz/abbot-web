using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class IntegrationsPage : OrganizationDetailPage
{
    readonly IIntegrationRepository _integrationRepository;
    readonly IZendeskInstaller _zendeskInstaller;
    readonly ISettingsManager _settingsManager;

    public IntegrationsPage(
        AbbotContext db,
        IIntegrationRepository integrationRepository,
        IZendeskInstaller zendeskInstaller,
        ISettingsManager settingsManager,
        IAuditLog auditLog) : base(db, auditLog)
    {
        _integrationRepository = integrationRepository;
        _zendeskInstaller = zendeskInstaller;
        _settingsManager = settingsManager;
    }

    public Integration? HubSpotIntegration { get; private set; }

    public HubSpotSettings? HubSpotSettings { get; private set; }

    public FormSettings? HubSpotFormSettings { get; private set; }

    public Integration? ZenDeskIntegration { get; private set; }

    public ZendeskSettings? ZendeskSettings { get; private set; }

    public Integration? SlackAppIntegration { get; private set; }

    public SlackAppSettings? SlackAppSettings { get; private set; }

    public async Task OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        (HubSpotIntegration, HubSpotSettings) = await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(organization);
        HubSpotFormSettings = await _settingsManager.GetHubSpotFormSettingsAsync(organization);
        (ZenDeskIntegration, ZendeskSettings) = await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);
        (SlackAppIntegration, SlackAppSettings) = await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);
    }

    public async Task<IActionResult> OnPostUninstallZendesk(string id, string subdomain)
    {
        var organization = await InitializeDataAsync(id);
        var (integration, settings) = await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);
        if (integration is null || settings is null)
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Integration not found.";
            return RedirectToPage();
        }

        if (settings.Subdomain != subdomain)
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Confirm Zendesk subdomain to uninstall.";
            return RedirectToPage();
        }

        if (settings is not { HasApiCredentials: true })
        {
            if (integration is { Enabled: true })
            {
                await _integrationRepository.DisableAsync(organization, IntegrationType.Zendesk, Viewer);
                StatusMessage = $"Integration disabled.";
            }
            else
            {
                StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot uninstall without credentials.";
            }
            return RedirectToPage();
        }

        await _zendeskInstaller.UninstallFromZendeskAsync(organization, settings);
        await _integrationRepository.SaveSettingsAsync(integration, settings);
        await _integrationRepository.DisableAsync(organization, IntegrationType.Zendesk, Viewer);

        StatusMessage = $"Integration uninstalled.";
        return RedirectToPage();
    }
}

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.Zendesk;

public class CredentialsPage : ZendeskPageBase
{
    readonly IZendeskInstaller _zendeskInstaller;
    readonly ILinkedIdentityRepository _linkedIdentityRepository;
    readonly IHostEnvironment _hostEnvironment;

    public CredentialsPage(IIntegrationRepository integrationRepository,
        IZendeskInstaller zendeskInstaller,
        ILinkedIdentityRepository linkedIdentityRepository,
        IHostEnvironment hostEnvironment,
        IOptions<ZendeskOptions> zendeskOptions) : base(integrationRepository, zendeskOptions)
    {
        _hostEnvironment = hostEnvironment;
        _zendeskInstaller = zendeskInstaller;
        _linkedIdentityRepository = linkedIdentityRepository;
    }

    public string? ExistingClientSecretPrefix { get; set; }

    [Required]
    [BindProperty]
    // This regex came from Zendesk support
    [RegularExpression(@"^[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?$")]
    public string? Subdomain { get; set; }

    public bool HasApiToken { get; set; }

    public string ClientName => _hostEnvironment.EnvironmentName.ToLowerInvariant() switch
    {
        "development" => $"Abbot (dev-{Environment.UserName})",
        "production" => "Abbot",
        var x => $"Abbot ({x})",
    };

    public async Task<IActionResult> OnGet(bool editing = false)
    {
        Subdomain = Settings.Subdomain;
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Changing the subdomain means we have to uninstall the integration and clear any linked identities.
        if (!string.Equals(Settings.Subdomain, Subdomain, StringComparison.OrdinalIgnoreCase) && Settings.HasApiCredentials)
        {
            await _zendeskInstaller.UninstallFromZendeskAsync(Organization, Settings);
            await IntegrationRepository.DisableAsync(Organization, IntegrationType.Zendesk, Viewer);
            await _linkedIdentityRepository.ClearIdentitiesAsync(Organization, LinkedIdentityType.Zendesk);
            StatusMessage = "Zendesk integration was uninstalled. Please reinstall it to continue.";
        }
        Settings.Subdomain = Subdomain;

        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);

        return RedirectToPage("/Settings/Organization/Integrations/Zendesk/Index");
    }
}

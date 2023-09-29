using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.SlackApp;

public class ManifestModel : SlackAppPageBase
{
    public ManifestModel(IIntegrationRepository integrationRepository) : base(integrationRepository)
    {
    }

    public void OnGet(bool editing)
    {
        Editing = editing;

        Manifest = Settings.Manifest ?? new();
    }

    public bool Editing { get; set; }

    [Required]
    [BindProperty]
    public SlackManifestSettings Manifest { get; set; } = null!;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Settings.Manifest = Manifest;

        // Save the settings
        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
        return RedirectWithStatusMessage("Manifest updated.", NextSetupPage());
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        Settings.Manifest = null;
        await IntegrationRepository.SaveSettingsAsync(Integration!, Settings);

        StatusMessage = "Custom Slack App manifest cleared";
        return RedirectToPage();
    }
}

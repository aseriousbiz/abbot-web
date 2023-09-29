using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Repositories;
using Serious.Cryptography;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.Ticketing;

public class CreatePage : UserPage
{
    readonly IIntegrationRepository _integrationRepository;

    public CreatePage(IIntegrationRepository integrationRepository)
    {
        _integrationRepository = integrationRepository;
    }

    [Required]
    [BindProperty]
    [Display(Name = "Access Token")]
    public SecretString? AccessToken { get; set; }

    [Required]
    [BindProperty]
    public TicketingAccountDetails AccountDetails { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || !AccountDetails.IsValid)
        {
            return Page();
        }

        var integration = await _integrationRepository.CreateIntegrationAsync(Organization, IntegrationType.Ticketing, false);
        integration.ExternalId = AccountDetails.Id;
        await _integrationRepository.SaveSettingsAsync(integration, new TicketingSettings
        {
            AccessToken = AccessToken,
            AccountDetails = AccountDetails,
        });

        return RedirectWithStatusMessage($"{AccountDetails.Integration} Ticketing integration created.", "../Index");
    }
}

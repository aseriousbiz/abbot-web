using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;
using Serious.Cryptography;
using Serious.Slack.Manifests;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.SlackApp;

public class CredentialsModel : SlackAppPageBase
{
    readonly ISlackIntegration _slackIntegration;

    public CredentialsModel(
        IIntegrationRepository integrationRepository,
        ISlackIntegration slackIntegration) : base(integrationRepository)
    {
        _slackIntegration = slackIntegration;
    }

    public async void OnGet(bool editing)
    {
        Editing = editing;

        SlackAppId = Integration.ExternalId;
        Credentials = Settings.Credentials ?? new SlackCredentials();

        var defaultManifest = await _slackIntegration.GetDefaultManifestAsync();
        var baseManifest = defaultManifest with
        {
            // Clear default descriptions/color
            DisplayInformation = new(""),
        };
        Manifest = _slackIntegration.GenerateManifest(baseManifest, Integration, Settings);
    }

    public bool Editing { get; set; }

    public Manifest? Manifest { get; private set; }

    [Required]
    [BindProperty]
    [Display(Name = "App ID")]
    public string? SlackAppId { get; set; }

    [Required]
    [BindProperty]
    public SlackCredentials Credentials { get; set; } = null!;

    public async Task<IActionResult> OnPostAsync()
    {
        if (IsInstalled && Integration.ExternalId != SlackAppId)
        {
            ModelState.AddModelError(nameof(SlackAppId), "Cannot change App ID while installed.");
        }

        if (Settings.Credentials is { HasCredentials: true } existingCredentials)
        {
            // We want to avoid overwriting a secret with a truncated version of itself
            Credentials.ClientSecret = ReplaceIfTruncated(Credentials.ClientSecret, existingCredentials.ClientSecret);
            Credentials.SigningSecret = ReplaceIfTruncated(Credentials.SigningSecret, existingCredentials.SigningSecret);
        }

        if (!ModelState.IsValid)
        {
            Editing = true;
            return Page();
        }

        Integration.ExternalId = SlackAppId;
        Settings.Credentials = Credentials;

        // Save the settings
        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
        return RedirectWithStatusMessage("Credentials updated.", NextSetupPage());
    }

    static SecretString? ReplaceIfTruncated(SecretString? maybeTruncated, SecretString existingSecret)
    {
        if (maybeTruncated is null)
            return null;

        var maybeTruncatedValue = maybeTruncated.Reveal();
        var ellipsesIndex = maybeTruncatedValue.IndexOf('�', StringComparison.Ordinal);
        if (ellipsesIndex is > 0)
        {
            var existingSecretValue = existingSecret.Reveal();
            if (existingSecretValue.StartsWith(maybeTruncatedValue[..ellipsesIndex], StringComparison.Ordinal))
                return existingSecret;
        }

        return maybeTruncated;
    }

    public async Task<IActionResult> OnPostClearCredentialsAsync()
    {
        if (IsInstalled)
        {
            StatusMessage = "Cannot clear credentials while Custom Slack App is installed.";
            return RedirectToPage();
        }

        Integration.ExternalId = null;
        Settings.Credentials = null;
        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
        StatusMessage = "Custom Slack App credentials cleared";
        return RedirectToPage();
    }
}

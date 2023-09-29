using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Cryptography;

namespace Serious.Abbot.Pages.Settings.Organization.Runners;

public class EditPage : UserPage
{
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly IOrganizationRepository _organizationRepository;

    public CodeLanguage Language { get; private set; }

    public string DisplayName { get; set; } = null!;

    [BindProperty]
    public Uri Endpoint { get; set; } = null!;

    [BindProperty]
    public string? ApiToken { get; set; }

    public EditPage(IDataProtectionProvider dataProtectionProvider, IOrganizationRepository organizationRepository)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _organizationRepository = organizationRepository;
    }

    public async Task<IActionResult> OnGetAsync(string? language)
    {
        if (!Enum.TryParse<CodeLanguage>(language, ignoreCase: true, out var lang))
        {
            return BadRequest();
        }

        DisplayName = IndexPage.ConfigurableLanguages[lang];

        var settings = Organization.Settings.SkillEndpoints.TryGetValue(lang, out var s)
            ? s : null;

        if (settings is not null)
        {
            Endpoint = settings.Url;

            if (settings.ApiToken is not null)
            {
                var secret = new SecretString(settings.ApiToken, _dataProtectionProvider);
                ApiToken = secret.Reveal();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? language)
    {
        if (!Enum.TryParse<CodeLanguage>(language, ignoreCase: true, out var lang))
        {
            return BadRequest();
        }

        DisplayName = IndexPage.ConfigurableLanguages[lang];

        // Save the organization
        await _organizationRepository.SetOverrideRunnerEndpointAsync(Organization, lang, new(Endpoint, ApiToken), Viewer);
        StatusMessage = $"{DisplayName} runner endpoint updated.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetAsync(string? language)
    {
        if (!Enum.TryParse<CodeLanguage>(language, ignoreCase: true, out var lang))
        {
            return BadRequest();
        }

        // Remove the endpoint from the organization settings.
        Organization.Settings.SkillEndpoints.Remove(lang);

        // Save organization settings
        await _organizationRepository.ClearOverrideRunnerEndpointAsync(Organization, lang, Viewer);
        return RedirectToPage("/Settings/Organization/Runners/Index");
    }

    static SkillRunnerEndpoint? SynthesizeSettings(string? legacyEndpoint)
    {
        if (legacyEndpoint is not null)
        {
            var (url, token) = TryExtractToken(legacyEndpoint);
            return new(url, token);
        }

        return null;
    }

    static (Uri CleanUrl, string Token) TryExtractToken(string url)
    {
        // Try to extract the '?code=' parameter as the token
        var codeIndex = url.IndexOf("?code=", StringComparison.Ordinal);
        if (codeIndex == -1)
        {
            return (new(url), string.Empty);
        }

        var cleanUrl = url.Substring(0, codeIndex);
        var token = url.Substring(codeIndex + 6);
        return (new(cleanUrl), token);
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

namespace Serious.Abbot.Controllers;

[Authorize(Policy = AuthorizationPolicies.RequireAdministratorRole)]
public class GitHubController : Controller
{
    public static readonly string CorrelationCookieName = ".Abbot.Correlation.GitHub";
    public static readonly TimeSpan CorrelationExpiry = TimeSpan.FromMinutes(15);

    readonly ICorrelationService _correlationService;
    readonly IClock _clock;
    readonly IOptions<GitHubOptions> _options;
    readonly IIntegrationRepository _integrationRepository;
    readonly IOrganizationRepository _organizationRepository;

#pragma warning disable CA5395 // No idea why it thinks get_StatusMessage is an action

    [TempData]
    public string? StatusMessage { get; set; }

#pragma warning restore CA5395

    public GitHubController(ICorrelationService correlationService, IClock clock, IOptions<GitHubOptions> options, IIntegrationRepository integrationRepository, IOrganizationRepository organizationRepository)
    {
        _correlationService = correlationService;
        _clock = clock;
        _options = options;
        _integrationRepository = integrationRepository;
        _organizationRepository = organizationRepository;
    }

    [HttpGet("/github/install/complete")]
    public async Task<IActionResult> CompleteAsync(
        [FromQuery(Name = "installation_id")] int installationId,
        [FromQuery(Name = "setup_action")] string setupAction,
        string code,
        string? state)
    {
        IActionResult AuthorizationFailed()
        {
            StatusMessage = "The GitHub authorization flow failed. Please try again.";
            return RedirectToPage("/Settings/Organization/Integrations/GitHub/Index");
        }

        if (!_correlationService.TryValidate(HttpContext,
            state,
            CorrelationCookieName,
            _clock.UtcNow,
            out var correlationToken))
        {
            return AuthorizationFailed();
        }

        var organizationId = correlationToken.OrganizationId;
        var member = HttpContext.RequireCurrentMember();
        if (member.OrganizationId != organizationId)
        {
            return AuthorizationFailed();
        }

        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            return AuthorizationFailed();
        }

        // I don't think we need the OAuth credential.
        // Instead, we'll validate the state and then store the installation ID.
        // Our App Key can be used to authenticate as the Installation.
        var integration =
            await _integrationRepository.EnsureIntegrationAsync(organization, IntegrationType.GitHub);

        var settings = _integrationRepository.ReadSettings<GitHubSettings>(integration);
        settings.InstallationId = installationId;

        await _integrationRepository.SaveSettingsAsync(integration, settings);
        StatusMessage = "The GitHub integration was successfully installed.";
        return RedirectToPage("/Settings/Organization/Integrations/GitHub/Index");
    }

    [HttpGet("/github/install/{organizationId}")]
    public IActionResult Install(int organizationId)
    {
        // First, authenticate the current user
        var member = HttpContext.RequireCurrentMember();
        if (member.OrganizationId != organizationId)
        {
            StatusMessage = "You are not authorized to perform this action";
            return RedirectToPage("/Index");
        }

        var state = _correlationService.EncodeAndSetNonceCookie(
            HttpContext,
            CorrelationCookieName,
            _clock.UtcNow,
            _clock.UtcNow.Add(CorrelationExpiry),
            OAuthAction.Install,
            organizationId);

        var appSlug = _options.Value.AppSlug.Require("Required setting 'GitHub:AppSlug' is missing");

        return Redirect($"https://github.com/apps/{appSlug}/installations/new?state={state}");
    }
}

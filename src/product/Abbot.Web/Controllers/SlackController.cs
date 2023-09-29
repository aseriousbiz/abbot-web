using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Onboarding;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;

namespace Serious.Abbot.Controllers;

[Route("slack")]
[AbbotWebHost]
// non-GET APIs that use cookie auth _must_ use an Anti-Froggery token to prevent CSRF attacks
[AutoValidateAntiforgeryToken]
public class SlackController : UserControllerBase
{
    readonly ISlackAuthenticator _slackAuthenticator;
    readonly ISlackResolver _slackResolver;
    readonly IBotInstaller _botInstaller;
    readonly ISlackIntegration _slackIntegration;
    readonly OnboardingService _onboardingService;

#pragma warning disable CA5395 // No idea why it thinks get_StatusMessage is an action

    [TempData]
    public string? StatusMessage { get; set; }

#pragma warning restore CA5395

    public SlackController(ISlackAuthenticator slackAuthenticator, ISlackResolver slackResolver, IBotInstaller botInstaller, ISlackIntegration slackIntegration, OnboardingService onboardingService)
    {
        _slackAuthenticator = slackAuthenticator;
        _slackResolver = slackResolver;
        _botInstaller = botInstaller;
        _slackIntegration = slackIntegration;
        _onboardingService = onboardingService;
    }

    [HttpGet("install")]
    public async Task<IActionResult> Install()
    {
        var action = Security.OAuthAction.Install;
        var correlationValue = _slackAuthenticator.GetStateAndSetCorrelationCookie(HttpContext, Organization.Id, action);
        var installUrl = await _slackAuthenticator.GetInstallUrlAsync(Organization, action, correlationValue);

        // Let's GOOOOO
        return Redirect(installUrl);
    }

    [HttpGet("install/complete")]
    public async Task<IActionResult> InstallCompleteAsync(string? code, string? state)
    {
        if (!_slackAuthenticator.TryValidateCorrelationValue(HttpContext, state, out var correlationToken)
            || code is not { Length: > 0 }
            || Organization.Id != correlationToken.OrganizationId)
        {
            StatusMessage = "The Slack authorization flow failed. Please try again.";

            if (correlationToken?.OAuthAction == Security.OAuthAction.InstallCustom)
                return RedirectToPage("/Settings/Organization/Integrations/SlackApp/Index");

            return RedirectToPage("/Index");
        }

        var credentials = await _slackAuthenticator.GetCredentialsAsync(Organization, correlationToken.OAuthAction);

        var installEvent = await _slackResolver.ResolveInstallEventFromOAuthResponseAsync(
            code,
            credentials.ClientId,
            credentials.ClientSecret,
            User);

        if (correlationToken.OAuthAction == Security.OAuthAction.InstallCustom)
        {
            await _slackIntegration.InstallAsync(installEvent, CurrentMember);

            StatusMessage = "Custom Slack App has been installed!";
            return RedirectToPage("/Settings/Organization/Integrations/SlackApp/Index");
        }

        await _botInstaller.InstallBotAsync(installEvent);

        if (await _onboardingService.UpdateOnboardingStateAsync(Organization, CurrentMember) is { } result)
        {
            return result;
        }

        // Ok, we're installed!
        // Now let's go to the installation complete page.
        return RedirectToPage("/Account/Install/Complete",
            new { teamId = installEvent.PlatformId, auth = correlationToken.OAuthAction });
    }
}

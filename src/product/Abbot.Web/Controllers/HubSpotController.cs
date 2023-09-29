using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

[Authorize(Policy = AuthorizationPolicies.RequireAdministratorRole)]
public class HubSpotController : Controller
{
    readonly IClock _clock;
    readonly IOptions<HubSpotOptions> _hubSpotOptions;
    readonly ICorrelationService _correlationService;
    readonly IOrganizationRepository _organizationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IHubSpotClientFactory _hubSpotClientFactory;
    readonly IDataProtectionProvider _dataProtectionProvider;
    static readonly ILogger<HubSpotController> Log = ApplicationLoggerFactory.CreateLogger<HubSpotController>();

    public static readonly string CorrelationCookieName = ".Abbot.Correlation.HubSpot";
    public static readonly TimeSpan CorrelationExpiry = TimeSpan.FromMinutes(15);

#pragma warning disable CA5395 // No idea why it thinks get_StatusMessage is an action
    [TempData]
    public string? StatusMessage { get; set; }
#pragma warning restore CA5395

    public HubSpotController(
        IClock clock,
        IOptions<HubSpotOptions> hubSpotOptions,
        ICorrelationService correlationService,
        IOrganizationRepository organizationRepository,
        IIntegrationRepository integrationRepository,
        IHubSpotClientFactory hubSpotClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _clock = clock;
        _hubSpotOptions = hubSpotOptions;
        _correlationService = correlationService;
        _organizationRepository = organizationRepository;
        _integrationRepository = integrationRepository;
        _hubSpotClientFactory = hubSpotClientFactory;
        _dataProtectionProvider = dataProtectionProvider;
    }

    [HttpGet("/hubspot/install/complete")]
    public async Task<IActionResult> CompleteAsync(string? code, string? state)
    {
        IActionResult AuthorizationFailed()
        {
            StatusMessage = "The HubSpot authorization flow failed. Please try again.";
            return RedirectToPage("/Settings/Organization/Integrations/HubSpot/Index");
        }

        if (code is not { Length: > 0 })
        {
            return AuthorizationFailed();
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

        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(organization);

        if (integration is null)
        {
            StatusMessage = "HubSpot integration is not properly configured.";
            return RedirectToPage("/Settings/Organization/Integrations/HubSpot");
        }

        var landingUrl = Url.Action("Complete",
                "HubSpot",
                new {
                },
                HttpContext.Request.Scheme,
                HttpContext.Request.Host.Value)
            .Require();

        var client = _hubSpotClientFactory.CreateOAuthClient();
        OAuthRedeemResponse redeemResponse;
        OAuthTokenInfo tokenInfo;
        try
        {
            var request = new OAuthCodeRedeemRequest(
                code,
                _hubSpotOptions.Value.ClientId.Require("Required setting 'HubSpot:ClientId' is missing"),
                _hubSpotOptions.Value.ClientSecret.Require("Required setting 'HubSpot:ClientSecret' is missing"),
                landingUrl);

            redeemResponse = await client.RedeemCodeAsync(request);
            tokenInfo = await client.GetTokenInfoAsync(redeemResponse.AccessToken);
        }
        catch (Refit.ApiException apiex)
        {
            Log.ErrorRedeemingOAuthCode(apiex);
            StatusMessage = "Failed to verify HubSpot account. Please try again.";
            return RedirectToPage("/Settings/Organization/Integrations/HubSpot/Index");
        }

        settings ??= new HubSpotSettings();
        settings.RedirectUri = landingUrl;
        settings.AccessToken = new SecretString(redeemResponse.AccessToken, _dataProtectionProvider);
        settings.RefreshToken = new SecretString(redeemResponse.RefreshToken, _dataProtectionProvider);
        settings.AccessTokenExpiryUtc = _clock.UtcNow.AddSeconds(redeemResponse.ExpiresInSeconds);
        settings.ApprovedScopes = tokenInfo.Scopes;
        settings.HubDomain = tokenInfo.HubDomain;
        integration.ExternalId = $"{tokenInfo.HubId}";

        await _integrationRepository.SaveSettingsAsync(integration, settings);
        StatusMessage = "The HubSpot integration was successfully installed.";
        return RedirectToPage("/Settings/Organization/Integrations/HubSpot/Index");
    }

    [HttpGet("/hubspot/install/{organizationId}")]
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

        var landingUrl = Url.Action("Complete",
            "HubSpot",
            new {
            },
            HttpContext.Request.Scheme,
            HttpContext.Request.Host.Value).Require();

        var scopes = _hubSpotOptions.Value.RequiredScopes;
        var clientId = _hubSpotOptions.Value.ClientId.Require("Required setting 'HubSpot:ClientId' is missing");

        var installUrl =
            $"https://app.hubspot.com/oauth/authorize?response_type=code&redirect_uri={landingUrl}&client_id={clientId}&scope={scopes}&state={state}";

        return Redirect(installUrl);
    }
}

static partial class HubSpotControllerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Failed to redeem OAuth code with HubSpot.")]
    public static partial void
        ErrorRedeemingOAuthCode(this ILogger<HubSpotController> logger, Exception ex);
}

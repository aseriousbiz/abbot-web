using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

// WARNING: Do not put a RouteAttribute here!
// The URL of the Webhook action is installed into any Zendesk account with our integration configured.
// So we explicitly configure it's route, and don't want RouteAttributes up here adding confusion.
// The Webhook action has an HttpPost attribute specifying it's route explicitly.
public class ZendeskController : Controller
{
    static readonly ILogger<ZendeskController> Log = ApplicationLoggerFactory.CreateLogger<ZendeskController>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly ILinkedIdentityRepository _linkedIdentityRepository;
    readonly IClock _clock;
    readonly IZendeskClientFactory _clientFactory;
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly IZendeskInstaller _zendeskInstaller;
    readonly IOptions<ZendeskOptions> _zendeskOptions;
    readonly IZendeskToSlackImporter _zendeskToSlackImporter;
    readonly ICorrelationService _correlationService;

    public static readonly string CorrelationCookieName = ".Abbot.Correlation.Zendesk";
    public static readonly TimeSpan CorrelationExpiry = TimeSpan.FromMinutes(15);

#pragma warning disable CA5395 // No idea why it thinks get_StatusMessage is an action
    [TempData]
    public string? StatusMessage { get; set; }
#pragma warning restore CA5395

    public ZendeskController(
        IOrganizationRepository organizationRepository,
        IIntegrationRepository integrationRepository,
        ILinkedIdentityRepository linkedIdentityRepository,
        IZendeskClientFactory clientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IZendeskInstaller zendeskInstaller,
        IOptions<ZendeskOptions> zendeskOptions,
        IZendeskToSlackImporter zendeskToSlackImporter,
        ICorrelationService correlationService,
        IClock clock)
    {
        _organizationRepository = organizationRepository;
        _integrationRepository = integrationRepository;
        _linkedIdentityRepository = linkedIdentityRepository;
        _clock = clock;
        _clientFactory = clientFactory;
        _dataProtectionProvider = dataProtectionProvider;
        _zendeskInstaller = zendeskInstaller;
        _zendeskOptions = zendeskOptions;
        _zendeskToSlackImporter = zendeskToSlackImporter;
        _correlationService = correlationService;
    }

    // WARNING: Changing this route will break currently installed Zendesk webhooks!
    [HttpPost("/zendesk/webhook/{organizationId}")]
    public async Task<IActionResult> WebhookAsync(int organizationId, [FromBody] WebhookPayload payload)
    {
        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            return NotFound($"Received Zendesk webhook for non-existent organization {organizationId}");
        }

        using var _ = Log.BeginOrganizationScope(organization);
        if (!organization.Enabled)
        {
            Log.OrganizationDisabled();
            return Content("No can do, boss. Organization is disabled.");
        }

        var (_, settings) = await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);

        if (settings is not { HasApiCredentials: true, WebhookToken.Length: > 0 })
        {
            Log.ZendeskIntegrationNotConfigured(organization.PlatformId);
            return BadRequest("Organization not configured for Zendesk integration");
        }

        // The webhook just serves as a trigger to run the Comment Syncer for the ticket.
        // Before we can do that though, we need to verify the payload.
        // We gave Zendesk a token to send us as a Bearer token, so we can use that to validate.
        var authHeader = AuthenticationHeaderValue.Parse(Request.Headers.Authorization);
        if (authHeader.Scheme != "Bearer" || authHeader.Parameter != settings.WebhookToken)
        {
            return Unauthorized("Bad webhook token");
        }

        // Since we validated the request via the token we gave Zendesk, we're opting NOT to verify the signature.
        // That does mean that we could be subject to a replay attack (someone intercepts the token and presents it themselves).
        // The risk of that is minimal though. We're going back to the source Zendesk APIs to sync comments anyway.

        // The ticket URL we get the in the webhook doesn't start with "https://", so we need to add that.
        var ticketUrl = payload.TicketUrl.StartsWith("https://", StringComparison.Ordinal)
            ? payload.TicketUrl
            : $"https://{payload.TicketUrl}";

        if (ZendeskTicketLink.Parse(ticketUrl) is not { } ticket)
        {
            return BadRequest("Bad ticket URL in Zendesk webhook");
        }

        _zendeskToSlackImporter.QueueZendeskCommentImport(
            organization,
            ticket,
            payload.TicketStatus,
            payload.CurrentUserId);

        return Content("I'll take it from here, boss!");
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("/zendesk/install/complete")]
    public async Task<IActionResult> CompleteAsync(string? code, string? state)
    {
        IActionResult AuthorizationFailed()
        {
            StatusMessage = "The Zendesk authorization flow failed. Please try again.";
            return RedirectToPage("/Settings/Organization/Integrations/Zendesk/Index");
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
            await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);

        if (integration is null || settings is not { Subdomain.Length: > 0 })
        {
            StatusMessage = "Zendesk integration is not properly configured.";
            return RedirectToPage("/Settings/Organization/Integrations/Zendesk");
        }

        var client = _clientFactory.CreateOAuthClient(settings.Subdomain);
        var clientId = _zendeskOptions.Value.ClientId.Require("Required setting 'Zendesk:ClientId' is missing");
        var clientSecret = _zendeskOptions.Value.ClientSecret.Require("Required setting 'Zendesk:ClientSecret' is missing");

        var landingUrl = Url.Action("Complete",
                             "Zendesk",
                             new { },
                             HttpContext.Request.Scheme,
                             HttpContext.Request.Host.Value)
                         .Require();

        var scopes = UrlEncoder.Default.Encode("read write");

        try
        {
            var response = await client.RedeemCodeAsync(code, clientId, clientSecret, landingUrl, scopes);
            settings.ApiToken = new SecretString(response.AccessToken, _dataProtectionProvider);
        }
        catch (ApiException apiex)
        {
            Log.ErrorRedeemingOAuthCode(apiex, settings.Subdomain, organization.PlatformId);
            StatusMessage = "Failed to verify Zendesk account. Please try again.";
            return RedirectToPage("/Settings/Organization/Integrations/Zendesk/Index");
        }

        // Before saving things and installing abbot, check that token we have is for an admin
        var apiClient = _clientFactory.CreateClient(settings);

        switch (correlationToken.OAuthAction)
        {
            // If we're just connecting an identity, we don't need them to be a Zendesk admin.
            case OAuthAction.Install:
                try
                {
                    var user = await apiClient.GetCurrentUserAsync();

                    // TODO: ZENDESK: Only relevant if we're installing.
                    if (user.Body?.Role != "admin")
                    {
                        StatusMessage = "You must be a Zendesk admin to install Abbot.";
                        return RedirectToPage("/Settings/Organization/Integrations/Zendesk/Index");
                    }
                }
                catch (ApiException apiex)
                {
                    Log.ErrorVerifyingUser(apiex, settings.Subdomain, organization.PlatformId);
                    StatusMessage = "Failed to verify Zendesk account. Please try again.";
                    return RedirectToPage("/Settings/Organization/Integrations/Zendesk/Index");
                }

                await _zendeskInstaller.InstallToZendeskAsync(organization, settings);
                await _integrationRepository.SaveSettingsAsync(integration, settings);

                StatusMessage = "The Zendesk integration was successfully installed.";
                return RedirectToPage("/Settings/Organization/Integrations/Zendesk/Index");
            case OAuthAction.Connect:
                // If we're here, we're just connecting an identity.
                try
                {
                    var currentUser = await apiClient.GetCurrentUserAsync();
                    if (currentUser.Body?.Url is not { Length: > 0 } externalId)
                    {
                        Log.ErrorRetrievingExternalId(member.Id, settings.Subdomain, organization.PlatformId);
                        StatusMessage = "Failed to verify Zendesk account. Please try again.";
                    }
                    else
                    {
                        var subdomain = ZendeskUserLink.Parse(currentUser.Body.Url).Require().Subdomain;
                        var zendeskMetadata = new ZendeskUserMetadata(currentUser.Body.Role,
                            subdomain);
                        await _linkedIdentityRepository.LinkIdentityAsync(
                            organization,
                            member,
                            LinkedIdentityType.Zendesk,
                            externalId,
                            currentUser.Body.Name,
                            zendeskMetadata);

                        StatusMessage = "Successfully linked your Zendesk account!";
                    }
                }
                catch (ApiException apiex)
                {
                    Log.ErrorVerifyingUser(apiex, settings.Subdomain, organization.PlatformId);
                    StatusMessage = "Failed to verify Zendesk account. Please try again.";

                }

                return RedirectToPage("/Settings/Account/Index");
            default:
                throw new UnreachableException();
        }
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("/zendesk/install/{organizationId}")]
    public async Task<IActionResult> InstallAsync(int organizationId)
    {
        return await InitiateAsync(organizationId, OAuthAction.Install);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("/zendesk/connect/{organizationId}")]
    public async Task<IActionResult> ConnectAsync(int organizationId)
    {
        return await InitiateAsync(organizationId, OAuthAction.Connect);
    }

    async Task<IActionResult> InitiateAsync(int organizationId, OAuthAction action)
    {
        // First, authenticate the current user
        var member = HttpContext.RequireCurrentMember();
        if (member.OrganizationId != organizationId)
        {
            StatusMessage = "You are not authorized to perform this action";
            return RedirectToPage("/Index");
        }

        var (_, settings) =
            await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(member.Organization);

        if (settings?.Subdomain is not { Length: > 0 })
        {
            if (member.IsInRole(Roles.Administrator))
            {
                StatusMessage = "Zendesk integration is not properly configured.";
                return RedirectToPage("/Settings/Organization/Integrations/Zendesk");
            }

            StatusMessage = "Zendesk integration is not properly configured. Contact an Administrator for help.";
            return RedirectToPage("/Index");
        }

        var state = _correlationService.EncodeAndSetNonceCookie(
            HttpContext,
            CorrelationCookieName,
            _clock.UtcNow,
            _clock.UtcNow.Add(CorrelationExpiry),
            action,
            organizationId);

        // Generate the Zendesk authenticate URL
        var landingUrl = Url.Action("Complete",
                             "Zendesk",
                             new { },
                             HttpContext.Request.Scheme,
                             HttpContext.Request.Host.Value)
                         .Require();

        var scopes = UrlEncoder.Default.Encode("read write");
        var clientId = _zendeskOptions.Value.ClientId.Require("Required setting 'Zendesk:ClientId' is missing");

        var installUrl =
            $"https://{settings.Subdomain}.zendesk.com/oauth/authorizations/new?response_type=code&redirect_uri={landingUrl}&client_id={clientId}&scope={scopes}&state={state}";

        return Redirect(installUrl);
    }
}

static partial class ZendeskControllerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Failed to redeem OAuth code with Zendesk tenant {ZendeskSubdomain} for organization {OrganizationPlatformId}.")]
    public static partial void
        ErrorRedeemingOAuthCode(this ILogger<ZendeskController> logger, Exception ex, string zendeskSubdomain,
            string organizationPlatformId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message =
            "Failed to verify user with Zendesk tenant {ZendeskSubdomain} for organization {OrganizationPlatformId}.")]
    public static partial void
        ErrorVerifyingUser(this ILogger<ZendeskController> logger, Exception ex, string zendeskSubdomain,
            string organizationPlatformId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message =
            "The ExternalId for the user MemberId {MemberId} is null for Zendesk tenant {ZendeskSubdomain} for organization {OrganizationPlatformId}.")]
    public static partial void ErrorRetrievingExternalId(
        this ILogger<ZendeskController> logger, int memberId, string zendeskSubdomain, string organizationPlatformId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message =
            "Received Webhook call for organization {OrganizationPlatformId}, but no Zendesk integration is configured.")]
    public static partial void
        ZendeskIntegrationNotConfigured(this ILogger<ZendeskController> logger, string organizationPlatformId);
}

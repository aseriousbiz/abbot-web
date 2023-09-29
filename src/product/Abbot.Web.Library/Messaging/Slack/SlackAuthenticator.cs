using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Slack.AspNetCore;

namespace Serious.Abbot.Messaging;

public interface ISlackAuthenticator
{
    string GetStateAndSetCorrelationCookie(HttpContext context, int organizationId, OAuthAction action);

    Task<SlackOAuthCredentials> GetCredentialsAsync(Organization organization, OAuthAction action);

    Task<string> GetInstallUrlAsync(Organization organization, OAuthAction action, string state);

    bool TryValidateCorrelationValue(HttpContext context, string? state, [NotNullWhen(true)] out CorrelationToken? correlationToken);
}

public class SlackAuthenticator : ISlackAuthenticator
{
    public static readonly string CorrelationCookieName = ".Abbot.Correlation.Slack";
    public static readonly TimeSpan CorrelationExpiry = TimeSpan.FromMinutes(15);

    readonly IIntegrationRepository _integrationRepository;
    readonly IOptions<SlackOptions> _slackOptions;
    readonly IUrlGenerator _urlGenerator;
    readonly IClock _clock;
    readonly ICorrelationService _correlationService;

    public SlackAuthenticator(
        IIntegrationRepository integrationRepository,
        IOptions<SlackOptions> slackOptions,
        IUrlGenerator urlGenerator,
        IClock clock,
        ICorrelationService correlationService)
    {
        _integrationRepository = integrationRepository;
        _slackOptions = slackOptions;
        _urlGenerator = urlGenerator;
        _clock = clock;
        _correlationService = correlationService;
    }

    public async Task<SlackOAuthCredentials> GetCredentialsAsync(Organization organization, OAuthAction action)
    {
        var (slackAppIntegration, customApp) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);

        switch (action)
        {
            case OAuthAction.InstallCustom:
            case OAuthAction.Install when organization is { BotAppId: { Length: > 0 } botAppId } && botAppId == slackAppIntegration?.ExternalId:
                if (customApp?.Credentials is not { } customCredentials)
                {
                    throw new UnreachableException("Attempted custom Slack install without credentials.");
                }

                return new SlackOAuthCredentials(
                    customCredentials.ClientId.Require(),
                    customCredentials.ClientSecret.Require().Reveal(),
                    (_slackOptions.Value.CustomAppScopes ?? _slackOptions.Value.RequiredScopes).Require());

            case OAuthAction.Install:
                return new SlackOAuthCredentials(
                    _slackOptions.Value.ClientId.Require(),
                    _slackOptions.Value.ClientSecret.Require(),
                    _slackOptions.Value.RequiredScopes.Require());

            default:
                throw new UnreachableException($"Unexpected OAuthAction {action}");
        }
    }

    public async Task<string> GetInstallUrlAsync(Organization organization, OAuthAction action, string state)
    {
        var credentials = await GetCredentialsAsync(organization, action);
        var redirectUri = _urlGenerator.SlackInstallComplete();
        var team = organization.PlatformId;
        return $"https://slack.com/oauth/v2/authorize?client_id={credentials.ClientId}&scope={credentials.Scopes}&redirect_uri={redirectUri}&team={team}&state={state}";
    }

    public string GetStateAndSetCorrelationCookie(HttpContext context, int organizationId, OAuthAction action) =>
        _correlationService.EncodeAndSetNonceCookie(context,
            CorrelationCookieName,
            _clock.UtcNow,
            _clock.UtcNow.Add(CorrelationExpiry),
            action,
            organizationId);

    public bool TryValidateCorrelationValue(HttpContext context, string? state, [NotNullWhen(true)] out CorrelationToken? correlationToken) =>
        _correlationService.TryValidate(context, state, CorrelationCookieName, _clock.UtcNow, out correlationToken);
}

public record SlackOAuthCredentials(string ClientId, string ClientSecret, string Scopes);

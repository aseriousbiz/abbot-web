using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class ApiExplorerModel : OrganizationDetailPage
{
    readonly IHttpClientFactory _httpClientFactory;
    readonly IIntegrationRepository _integrationRepository;
    readonly IHubSpotClientFactory _hubSpotClientFactory;
    readonly IGitHubClientFactory _gitHubClientFactory;
    readonly IMergeDevClientFactory _mergeDevClientFactory;
    public DomId ResultsAreaId { get; } = new("results-area");

    public IReadOnlyList<SelectListItem> Apis { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ApiExplorerModel(
        IHttpClientFactory httpClientFactory,
        IIntegrationRepository integrationRepository,
        IHubSpotClientFactory hubSpotClientFactory,
        IGitHubClientFactory gitHubClientFactory,
        IMergeDevClientFactory mergeDevClientFactory,
        AbbotContext db,
        IAuditLog auditLog)
        : base(db, auditLog)
    {
        _httpClientFactory = httpClientFactory;
        _integrationRepository = integrationRepository;
        _hubSpotClientFactory = hubSpotClientFactory;
        _gitHubClientFactory = gitHubClientFactory;
        _mergeDevClientFactory = mergeDevClientFactory;
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        var integrations = await _integrationRepository.GetIntegrationsAsync(organization);
        Apis = GenerateApis(organization, integrations).ToList();
    }

    public async Task OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        await InitializeDataAsync(id);

        var (apiOrError, auditEventName, integration, authenticate) = await ParseApi(Input.Api);

        if (authenticate is null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Api)}", apiOrError);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Record an audit event
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new($"Organization.Api.{auditEventName}", "Queried"),
                Description = $"Queried {apiOrError} API URL {Input.Url}",
                Actor = Viewer,
                Organization = Organization,
                StaffPerformed = true,
                StaffOnly = true,
                StaffReason = Input.Reason,
                Properties = new {
                    IntegrationId = integration?.Id
                }
            });

        // We're just gonna use a standard HTTP client here.
        // We have typed clients for a lot of the APIs we use, but this is a raw staff tool
        // so we don't need to go to that level of abstraction.
        var client = _httpClientFactory.CreateClient("StaffApiExplorer");

        using var req = new HttpRequestMessage(HttpMethod.Get, Input.Url);
        authenticate!(req);

        // Make the request
        var response = await client.SendAsync(req);

        // We assume everything is JSON. So load it up into a JToken and reformat it.
        var json = await response.Content.ReadAsStringAsync();
        string responseBody = string.Empty;
        try
        {
            var parsed = JToken.Parse(json);
            responseBody = parsed.ToString(Formatting.Indented);
        }
        catch (JsonReaderException e)
        {
            responseBody = e + Environment.NewLine + Environment.NewLine + json;
        }

        var result = $"{(int)response.StatusCode} {response.ReasonPhrase}\n";
        foreach (var (name, values) in response.Headers)
        {
            result += $"{name}: {string.Join(", ", values)}\n";
        }

        result += "\n";
        result += responseBody;

        return TurboUpdate(ResultsAreaId, result);
    }

    IEnumerable<SelectListItem> GenerateApis(Organization organization, IEnumerable<Integration> integrations)
    {
        if (organization.HasApiToken())
        {
            yield return new SelectListItem(
                $"Organization {organization.PlatformType.Humanize()}",
                "Platform");
        }

        foreach (var integration in integrations)
        {
            switch (integration.Type)
            {
                case IntegrationType.SlackApp when
                    _integrationRepository.ReadSettings<SlackAppSettings>(integration) is { } settings:
                    if (settings.Authorization.HasApiToken())
                    {
                        yield return new SelectListItem(
                            integration.Type.Humanize(),
                            $"{integration.Id}:Custom");
                    }
                    if (settings.DefaultAuthorization.HasApiToken())
                    {
                        yield return new SelectListItem(
                            "Default Slack App",
                            $"{integration.Id}:Default");
                    }
                    break;
                case IntegrationType.HubSpot when
                    _integrationRepository.ReadSettings<HubSpotSettings>(integration) is { } settings
                    && settings is { HasApiCredentials: true }:
                    yield return new SelectListItem(
                        integration.Type.Humanize(),
                        $"{integration.Id}");
                    break;
                case IntegrationType.Zendesk when
                    _integrationRepository.ReadSettings<ZendeskSettings>(integration) is { } settings
                    && settings is { HasApiCredentials: true }:
                    yield return new SelectListItem(
                        integration.Type.Humanize(),
                        $"{integration.Id}");
                    break;
                case IntegrationType.GitHub when
                    _integrationRepository.ReadSettings<GitHubSettings>(integration) is { } settings
                    && settings is { HasApiCredentials: true }:
                    yield return new SelectListItem(
                        $"{integration.Type.Humanize()}",
                        $"{integration.Id}");
                    break;
                case IntegrationType.Ticketing when
                    _integrationRepository.ReadSettings<TicketingSettings>(integration) is { } settings
                    && settings is { HasApiCredentials: true }:
                    yield return new SelectListItem(
                        $"{integration.Type.Humanize()} in {settings.IntegrationName}",
                        $"{integration.Id}");
                    break;
            }
        }
    }

    private async Task<(string, string?, Integration?, Action<HttpRequestMessage>?)> ParseApi(string api)
    {
        if (api == "Platform"
            && Organization.TryGetUnprotectedApiToken(out var accessToken))
        {
            return (
                $"Organization {Organization.PlatformType.Humanize()}",
                "Slack",
                null,
                req => req.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken)
            );
        }

        var splat = api.Split(':');
        if (!int.TryParse(splat[0], out var integrationId)
            || await _integrationRepository.GetIntegrationByIdAsync(integrationId) is not { } integration
            || integration.OrganizationId != Organization.Id)
        {
            return ("Integration not found.", null, null, null);
        }

        return integration.Type switch
        {
            IntegrationType.SlackApp when
                _integrationRepository.ReadSettings<SlackAppSettings>(integration) is { } settings => splat switch
                {
                    [_, "Custom"] when
                        settings.Authorization is { } auth
                        && auth.TryGetUnprotectedApiToken(out var apiToken) =>
                        (
                            integration.Type.Humanize(),
                            "Slack.Custom",
                            integration,
                            req => req.Headers.Authorization =
                                new AuthenticationHeaderValue("Bearer", apiToken)
                        ),
                    [_, "Default"] when
                        settings.DefaultAuthorization is { } auth
                        && auth.TryGetUnprotectedApiToken(out var apiToken) =>
                        (
                            "Default Slack App",
                            "Slack.Default",
                            integration,
                            req => req.Headers.Authorization =
                                new AuthenticationHeaderValue("Bearer", apiToken)
                        ),
                    _ => ("Slack API Token not found.", null, null, null),
                },
            IntegrationType.HubSpot when
                _integrationRepository.ReadSettings<HubSpotSettings>(integration) is { } settings
                && settings is { HasApiCredentials: true }
                && await _hubSpotClientFactory.GetOrRenewAccessTokenAsync(integration, settings) is { } apiToken =>
                (
                    integration.Type.Humanize(),
                    "HubSpot",
                    integration,
                    req => req.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", apiToken.Reveal())
                ),
            IntegrationType.Zendesk when
                _integrationRepository.ReadSettings<ZendeskSettings>(integration) is { } settings
                && settings is { HasApiCredentials: true } =>
                (
                    integration.Type.Humanize(),
                    "Zendesk",
                    integration,
                    req => req.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", settings.ApiToken.Reveal())
                ),
            IntegrationType.GitHub when
                _integrationRepository.ReadSettings<GitHubSettings>(integration) is { } settings
                && settings is { HasApiCredentials: true }
                && await _gitHubClientFactory.GetOrRenewAccessTokenAsync(integration, settings) is { } apiToken =>
                (
                    integration.Type.Humanize(),
                    "GitHub",
                    integration,
                    req => _gitHubClientFactory.ApplyAuthorization(req, apiToken.Reveal())
                ),
            IntegrationType.Ticketing when
                _integrationRepository.ReadSettings<TicketingSettings>(integration) is { } settings
                && settings is { HasApiCredentials: true } =>
                (
                    $"{integration.Type.Humanize()} in {settings.IntegrationName}",
                    "Ticketing",
                    integration,
                    req => _mergeDevClientFactory.ApplyAuthorization(req, settings.AccessToken.Reveal())
                ),
            _ => ($"{integration.Type.Humanize()} API Token not found.", null, null, null),
        };
    }

    public class InputModel
    {
        [Required]
        [BindProperty]
        public string Api { get; set; } = string.Empty;

        [Required]
        [BindProperty]
        public string Url { get; set; } = string.Empty;

        [Required]
        [BindProperty]
        public string Reason { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore;
using Serious.Slack;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class StatusPage : OrganizationDetailPage
{
    readonly ISlackApiClient _slackClient;
    readonly IIntegrationRepository _integrationRepository;
    readonly ISettingsManager _settingsManager;
    readonly IHostEnvironment _hostEnvironment;
    readonly FeatureService _featureService;

    public DomId SettingsListDomId { get; } = new("settings-list");

    public StatusPage(
        AbbotContext db,
        ISlackApiClient slackClient,
        IIntegrationRepository integrationRepository,
        ISettingsManager settingsManager,
        IHostEnvironment hostEnvironment,
        FeatureService featureService,
        IAuditLog auditLog)
        : base(db, auditLog)
    {
        _slackClient = slackClient;
        _integrationRepository = integrationRepository;
        _settingsManager = settingsManager;
        _hostEnvironment = hostEnvironment;
        _featureService = featureService;
    }

    public IReadOnlyList<FieldInfo> Fields { get; private set; } = Array.Empty<FieldInfo>();

    /// <summary>
    /// If <c>true</c>, the current organization has a non-empty api token.
    /// </summary>
    public bool HasApiToken { get; private set; }

    public HubSpotSettings? HubSpotSettings { get; private set; }

    public ZendeskSettings? ZendeskSettings { get; private set; }

    public bool ShowApiTokens => _hostEnvironment.IsDevelopment() && Request.IsLocal() || Organization.IsSerious();

    public TargetingContext? FeatureContext { get; private set; }

    public IReadOnlyList<FeatureState> FeatureState { get; private set; } = Array.Empty<FeatureState>();

    public async Task OnGetAsync(string id)
    {
        var organization = await InitializeDataAsync(id);
        if (organization is null)
        {
            throw new InvalidOperationException($"Organization not found for id {id}.");
        }

        FeatureContext = organization.GetTargetingContext();
        FeatureState = await _featureService.GetFeatureStateAsync(organization);

        if (ShowApiTokens)
        {
            var (_, hubSpotSettings) = await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(organization);

            HubSpotSettings = hubSpotSettings;

            var (_, zendeskSettings) = await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);

            ZendeskSettings = zendeskSettings;
        }

        HasApiToken = organization.HasApiToken();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        await InitializeOrganizationAsync(id);

        if (Organization is { PlatformType: PlatformType.Slack })
        {
            await UpdateValuesForSlackAsync(Organization);
        }

        return RedirectToPage();
    }

    public SettingsScope Scope() => SettingsScope.Organization(Organization);

    public async Task<IActionResult> OnPostSettingsAsync(string id)
    {
        var organization = await InitializeDataAsync(id);
        if (organization is null)
        {
            throw new InvalidOperationException($"Organization not found for id {id}.");
        }

        var settings = await _settingsManager.GetAllAsync(Scope());
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    public async Task<IActionResult> OnPostSettingDeleteAsync(string id, string name)
    {
        var organization = await InitializeDataAsync(id);
        if (organization is null)
        {
            return NotFound($"Organization not found for id {id}");
        }

        var scope = Scope();
        if (await _settingsManager.GetAsync(scope, name) is not { } setting)
        {
            return TurboFlash($"Setting '{name}' not found.");
        }

        await _settingsManager.RemoveWithAuditingAsync(scope, name, Viewer.User, organization);

        var settings = await _settingsManager.GetAllAsync(scope);
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    async Task UpdateValuesForSlackAsync(Organization organization)
    {
        if (await GetSlackPlatformData(organization) is not var (authTestResponse, botInfo, botUserInfo, _))
        {
            return;
        }

        // Don't touch PlatformId. We trust that one.
        // In fact, refuse to update if the PlatformId of the token doesn't match the one in the DB
        if (authTestResponse.TeamId != organization.PlatformId)
        {
            StatusMessage = "Cannot update organization, PlatformId is incorrect.";
            return;
        }

        var somethingChanged = false;
        if (authTestResponse.BotId is { Length: > 0 })
        {
            organization.PlatformBotId = authTestResponse.BotId;
            somethingChanged = true;
        }
        if (authTestResponse.UserId is { Length: > 0 })
        {
            organization.PlatformBotUserId = authTestResponse.UserId;
            somethingChanged = true;
        }
        if (botUserInfo is { Body: { } botUser })
        {
            organization.BotName = GetBotName(botUser);
            somethingChanged = true;
        }

        var botAvatar = botInfo?.Body?.Icons?.Image72 ?? botInfo?.Body?.Icons?.Image48 ?? botInfo?.Body?.Icons?.Image36;
        if (botAvatar is { Length: > 0 })
        {
            organization.BotAvatar = botAvatar;
            somethingChanged = true;
        }

        if (botInfo is { Body.Name: var botAppName })
        {
            organization.BotAppName = botAppName;
            somethingChanged = true;
        }
        if (botInfo is { Body.AppId: var botAppId })
        {
            organization.BotAppId = botAppId;
            somethingChanged = true;
        }

        if (somethingChanged)
        {
            Db.Organizations.Update(organization);
            await Db.SaveChangesAsync();
            StatusMessage = "Saved changes to organization";
        }
        else
        {
            StatusMessage = "Database is in-sync with the chat platform!";
        }
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        if (!(organization is { PlatformType: PlatformType.Slack }
            && await InitializeExpectedValuesForSlackAsync(organization)))
        {
            Fields = new[]
            {
                new FieldInfo("PlatformId", organization.PlatformId, null),
                new FieldInfo("PlatformBotId", organization.PlatformBotId, null),
                new FieldInfo("PlatformBotUserId", organization.PlatformBotUserId, null),
                new FieldInfo("BotName", organization.BotName, null),
                new FieldInfo("BotAvatar", organization.BotAvatar, null),
                new FieldInfo("BotAppName", organization.BotAppName, null),
                new FieldInfo("BotAppId", organization.BotAppId, null),
                new FieldInfo("Scopes", organization.Scopes, null),
            };
        }
    }

    async Task<bool> InitializeExpectedValuesForSlackAsync(Organization organization)
    {
        if (await GetSlackPlatformData(organization) is not var (authTestResponse, botInfo, botUserInfo, scopes))
        {
            return false;
        }

        Fields = new[]
        {
            new FieldInfo("PlatformId", organization.PlatformId, authTestResponse.TeamId),
            new FieldInfo("PlatformBotId", organization.PlatformBotId, authTestResponse.BotId),
            new FieldInfo("PlatformBotUserId", organization.PlatformBotUserId, authTestResponse.UserId),
            new FieldInfo("BotName", organization.BotName, GetBotName(botUserInfo?.Body)),
            new FieldInfo("BotAvatar", organization.BotAvatar, botInfo?.Body?.Icons?.Image72 ?? botInfo?.Body?.Icons?.Image48 ?? botInfo?.Body?.Icons?.Image36),
            new FieldInfo("BotAppName", organization.BotAppName, botInfo?.Body?.Name),
            new FieldInfo("BotAppId", organization.BotAppId, botInfo?.Body?.AppId),
            new FieldInfo("Scopes", organization.Scopes, scopes),
        };

        return true;
    }

    private static string? GetBotName(UserInfo? botUserInfo) =>
        botUserInfo?.RealName ?? botUserInfo?.Name ?? botUserInfo?.UserName;

    async Task<(AuthTestResponse authTestResponse, BotInfoResponse? botInfo, UserInfoResponse? botUserInfo, string scopes)?> GetSlackPlatformData(Organization organization)
    {
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return null;
        }

        // Auth test first
        var response = await _slackClient.AuthTestWithScopesAsync(apiToken);
        var scopes = response.Scopes;
        var authTestResponse = response.ApiResponse.Require();

        // Now pull up info on the bot and user
        var botInfo = authTestResponse.BotId is { Length: > 0 }
            ? await _slackClient.GetBotsInfoAsync(apiToken, authTestResponse.BotId)
            : null;
        var botUserInfo = authTestResponse.UserId is { Length: > 0 }
            ? await _slackClient.GetUserInfo(apiToken, authTestResponse.UserId)
            : null;
        return (authTestResponse, botInfo, botUserInfo, scopes);
    }

    public record FieldInfo(
        string Name,
        string? Database,
        string? Platform);
}

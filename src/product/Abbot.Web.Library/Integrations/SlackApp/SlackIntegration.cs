using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.Slack.Manifests;

namespace Serious.Abbot.Integrations.SlackApp;

public interface ISlackIntegration
{
    Task<Manifest> GetDefaultManifestAsync();

    Manifest? GenerateManifest(Manifest baseManifest, Integration integration, SlackAppSettings settings);

    Task InstallAsync(InstallEvent installEvent, Member actor);

    Task UninstallAsync(Organization organization, Member actor);

    Task EnableAsync(Organization organization, Member actor);

    Task DisableAsync(Organization organization, Member actor);

    Task<SlackAuthorization> GetAuthorizationAsync(Organization organization, int? integrationId);

    Task<bool> HasRoomMembershipAsync(SlackAuthorization? auth);

    Task<IReadOnlyList<(ConversationInfoItem, bool?)>?> GetRoomMembershipAsync(SlackAppSettings settings);

    Task<int> InviteUserToRoomsAsync(SecretString inviterToken, string botUserId, IList<string> roomIds);
}

public class SlackIntegration : ISlackIntegration
{
    readonly ILogger<SlackIntegration> _logger;
    readonly IAuditLog _auditLog;
    readonly IOrganizationRepository _organizationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IBackgroundJobClient _jobClient;
    readonly IUrlGenerator _urlGenerator;
    readonly ISlackApiClient _slackApiClient;

    public SlackIntegration(
        ILogger<SlackIntegration> logger,
        IAuditLog auditLog,
        IOrganizationRepository organizationRepository,
        IIntegrationRepository integrationRepository,
        IBackgroundJobClient jobClient,
        IUrlGenerator urlGenerator,
        ISlackApiClient slackApiClient)
    {
        _logger = logger;
        _auditLog = auditLog;
        _organizationRepository = organizationRepository;
        _integrationRepository = integrationRepository;
        _jobClient = jobClient;
        _urlGenerator = urlGenerator;
        _slackApiClient = slackApiClient;
    }

    public async Task<Manifest> GetDefaultManifestAsync()
    {
        var manifestJson = await GetType().ReadResourceAsync("manifest.json");
        return SlackSerializer.Deserialize<Manifest>(manifestJson).Require();
    }

    public Manifest? GenerateManifest(Manifest baseManifest, Integration integration, SlackAppSettings settings)
    {
        if (settings.Manifest is not { IsValid: true } customManifest)
        {
            return null;
        }

        var features = baseManifest.Features.Require();
        var botUser = features.BotUser.Require();
        var oauthConfig = baseManifest.OAuthConfig.Require();
        var baseSettings = baseManifest.Settings.Require();
        var eventSubscriptions = baseSettings.EventSubscriptions.Require();
        var interactivity = baseSettings.Interactivity.Require();

        var requestUrl = _urlGenerator.SlackWebhookEndpoint(integration).ToString();
        return new Manifest
        {
            DisplayInformation = baseManifest.DisplayInformation with
            {
                Name = customManifest.AppName,
            },
            Features = baseManifest.Features with
            {
                BotUser = botUser with
                {
                    DisplayName = customManifest.BotUserDisplayName,
                },
            },
            OAuthConfig = oauthConfig with
            {
                RedirectUrls = new()
                {
                    _urlGenerator.SlackInstallComplete().ToString(),
                    _urlGenerator.Auth0LoginCallback().ToString(),
                },
            },
            Settings = baseSettings with
            {
                EventSubscriptions = eventSubscriptions with
                {
                    RequestUrl = requestUrl,
                },
                Interactivity = interactivity with
                {
                    RequestUrl = requestUrl,
                    MessageMenuOptionsUrl = requestUrl,
                },
            }
        };
    }

    public async Task InstallAsync(InstallEvent installEvent, Member actor)
    {
        var organization = await _organizationRepository.GetAsync(installEvent.PlatformId).Require();
        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);

        Expect.NotNull(integration);
        Expect.NotNull(settings);

        var auth = new SlackAuthorization(
            installEvent.AppId,
            installEvent.BotAppName,
            installEvent.BotId,
            installEvent.BotUserId,
            installEvent.BotName,
            installEvent.BotAvatar,
            BotResponseAvatar: null,
            installEvent.ApiToken,
            installEvent.OAuthScopes);

        await _integrationRepository.SaveSettingsAsync(integration,
            settings with
            {
                Authorization = auth,

                // Refresh default (system) auth, if it's not this app
                DefaultAuthorization =
                    organization.BotAppId is not null
                    && organization.BotAppId != installEvent.AppId
                        ? new(organization)
                        : settings.DefaultAuthorization,
            });

        _logger.AbbotInstalled(
            organization.Name, organization.Id,
            installEvent.PlatformId, installEvent.BotAppName, installEvent.AppId);

        var info = InstallationInfo.Create(InstallationEventAction.Install, auth);
        await _auditLog.LogInstalledAbbotAsync(info, organization, actor);
    }

    public async Task UninstallAsync(Organization organization, Member actor)
    {
        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);

        Expect.True(integration is { Enabled: false });

        if (settings is { Authorization: { } auth })
        {
            await _integrationRepository.SaveSettingsAsync(integration,
                settings with
                {
                    Authorization = null,
                    DefaultAuthorization = null,
                });

            // TODO: if ApiToken, try to uninstall via API

            _logger.AbbotUninstalled(
                organization.Name, organization.Id,
                organization.PlatformId, auth.AppName, auth.AppId);

            var info = InstallationInfo.Create(InstallationEventAction.Uninstall, auth);
            await _auditLog.LogUninstalledAbbotAsync(info, organization, actor);
        }
    }

    public async Task EnableAsync(Organization organization, Member actor)
    {
        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);

        Expect.NotNull(integration);
        Expect.True(settings?.HasAuthorization(integration) is true);

        // Shouldn't be possible to Enable if the Org is already configured for this app,
        // but let's avoid overwriting DefaultAuthorization with the custom Auth if it is.
        if (organization.BotAppId != integration.ExternalId)
        {
            await _integrationRepository.SaveSettingsAsync(integration,
                settings with
                {
                    DefaultAuthorization = new(organization),
                });
        }

        settings.Authorization.Apply(organization);
        await _organizationRepository.SaveChangesAsync();
        await _integrationRepository.EnableAsync(organization, IntegrationType.SlackApp, actor);

        _jobClient.Enqueue<OrganizationApiSyncer>(s => s.UpdateRoomsFromApiAsync(organization.Id));
    }

    public async Task DisableAsync(Organization organization, Member actor)
    {
        var (_, settings) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);

        var defaultAuth = settings?.DefaultAuthorization ?? new();
        defaultAuth.Apply(organization);
        await _organizationRepository.SaveChangesAsync();
        await _integrationRepository.DisableAsync(organization, IntegrationType.SlackApp, actor);

        _jobClient.Enqueue<OrganizationApiSyncer>(s => s.UpdateRoomsFromApiAsync(organization.Id));
    }

    public async Task<SlackAuthorization> GetAuthorizationAsync(Organization organization, int? integrationId)
    {
        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);

        // No integrationId means we should respond with default (system) app auth
        if (integrationId is null)
        {
            // If custom app is enabled for the Org...
            if (organization.BotAppId is { } orgAppId
                && orgAppId == integration?.ExternalId
                && settings is not null)
            {
                // We should use the saved default bot authorization
                if (settings.DefaultAuthorization is { } defaultAuth)
                {
                    return defaultAuth;
                }

                // No default auth is saved, and we can't use Org auth because it's the custom app
                _logger.DefaultAuthorizationMissing(organization.PlatformId, integration.Require().Id);
                return new();
            }

            // Custom app is not enabled, so use Org auth (if set)
            return new(organization);
        }

        // Request is for a custom app, but we can't match it with the Org!
        if (integrationId != integration?.Id)
        {
            if (integration is null)
            {
                _logger.IntegrationNotFound(integrationId, organization.PlatformId);
            }
            else
            {
                _logger.IntegrationMismatch(integrationId, organization.PlatformId, integration.Id);
            }
            return new();
        }

        // Use custom auth from the matching Integration (if set)
        if (settings?.Authorization is { } auth)
        {
            return auth;
        }

        _logger.CustomAuthorizationMissing(organization.PlatformId, integration.Require().Id);
        return new();
    }

    public async Task<bool> HasRoomMembershipAsync(SlackAuthorization? auth)
    {
        if (auth is not { ApiToken.Empty: false })
            return false;

        var response =
            await _slackApiClient.GetUsersConversationsAsync(
                auth.ApiToken.Reveal(),
                limit: 1,
                auth.BotUserId,
                "public_channel,private_channel",
                excludeArchived: true);

        if (!response.Ok)
        {
            _logger.SlackErrorReceived(response.Error);
            return false;
        }

        return response.Body.Any();
    }

    public async Task<IReadOnlyList<(ConversationInfoItem, bool?)>?> GetRoomMembershipAsync(SlackAppSettings settings)
    {
        var defaultRooms = await GetAllUsersConversationsAsync(settings.DefaultAuthorization, excludeArchived: true);

        if (defaultRooms is null)
        {
            return null;
        }

        var customRooms = (await GetAllUsersConversationsAsync(settings.Authorization, excludeArchived: true))
            ?.ToDictionary(ci => ci.Id);

        return defaultRooms
            .Select(ci => (Room: ci, IsMember: customRooms?.ContainsKey(ci.Id)))
            .OrderBy(x => x.IsMember).ThenBy(x => x.Room.Name)
            .ToList();
    }

    Task<IReadOnlyList<ConversationInfoItem>?> GetAllUsersConversationsAsync(
        SlackAuthorization? auth,
        string types = "public_channel,private_channel",
        bool excludeArchived = false) =>
        GetAllUsersConversationsAsync(auth?.ApiToken, auth?.BotUserId, types, excludeArchived);

    async Task<IReadOnlyList<ConversationInfoItem>?> GetAllUsersConversationsAsync(
        SecretString? apiToken,
        string? userId,
        string types = "public_channel,private_channel",
        bool excludeArchived = false)
    {
        if (apiToken is not { Empty: false } || userId is null)
        {
            return null;
        }

        var response =
            await _slackApiClient.GetAllUsersConversationsAsync(
                apiToken.Reveal(),
                userId,
                types,
                teamId: null,
                excludeArchived);

        if (response.Ok)
        {
            return response.Body;
        }

        _logger.SlackErrorReceived(response.Error);
        return null;
    }

    public async Task<int> InviteUserToRoomsAsync(SecretString inviterToken, string botUserId, IList<string> roomIds)
    {
        var apiToken = inviterToken.Reveal();

        var successCount = 0;
        foreach (var roomId in roomIds)
        {
            var response = await _slackApiClient.Conversations.InviteUsersToConversationAsync(
                apiToken,
                new(roomId, new[] { botUserId }));

            if (!response.Ok)
            {
                _logger.SlackErrorReceived(response.Error);
            }
            else
            {
                successCount++;
            }
        }

        return successCount;
    }
}

static partial class SlackIntegrationLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Callback integrationId ({callbackIntegrationId}) not found for {PlatformTeamId}")]
    public static partial void IntegrationNotFound(this ILogger<SlackIntegration> logger, int? callbackIntegrationId, string? platformTeamId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Callback integrationId ({callbackIntegrationId}) does not match {PlatformTeamId} (Integration {IntegrationId})")]
    public static partial void IntegrationMismatch(this ILogger<SlackIntegration> logger, int? callbackIntegrationId, string? platformTeamId, int integrationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Default Slack Authorization missing for {PlatformTeamId} (Integration {IntegrationId})")]
    public static partial void DefaultAuthorizationMissing(this ILogger<SlackIntegration> logger, string? platformTeamId, int integrationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Custom Slack Authorization missing for {PlatformTeamId} (Integration {IntegrationId})")]
    public static partial void CustomAuthorizationMissing(this ILogger<SlackIntegration> logger, string? platformTeamId, int integrationId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message =
            "Custom Slack App installed to organization ((Name: {OrganizationName}, Id: {OrganizationId}, PlatformId: {PlatformId}, BotApp: {BotAppName} ({BotAppId}))")]
    public static partial void AbbotInstalled(
        this ILogger<SlackIntegration> logger,
        string? organizationName,
        int organizationId,
        string platformId,
        string? botAppName,
        string? botAppId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message =
            "Custom Slack App uninstalled from organization (Name: {OrganizationName}, Id: {OrganizationId}, PlatformId: {PlatformId}, BotApp: {BotAppName} ({BotAppId}))")]
    public static partial void AbbotUninstalled(
        this ILogger<SlackIntegration> logger,
        string? organizationName,
        int organizationId,
        string platformId,
        string? botAppName,
        string? botAppId);
}

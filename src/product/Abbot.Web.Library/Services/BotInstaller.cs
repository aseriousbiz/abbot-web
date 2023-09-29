using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Services;

public interface IBotInstaller
{
    Task InstallBotAsync(InstallEvent installEvent);

    Task UninstallBotAsync(IPlatformEvent uninstallEvent);
}

public class BotInstaller : IBotInstaller
{
    readonly IOrganizationRepository _organizationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IAuditLog _auditLog;
    readonly IPublishEndpoint _publishEndpoint;
    readonly ILogger<BotInstaller> _logger;
    readonly IClock _clock;

    public BotInstaller(IOrganizationRepository organizationRepository,
        IIntegrationRepository integrationRepository,
        IAuditLog auditLog,
        IPublishEndpoint publishEndpoint,
        ILogger<BotInstaller> logger,
        IClock clock)
    {
        _organizationRepository = organizationRepository;
        _integrationRepository = integrationRepository;
        _auditLog = auditLog;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _clock = clock;
    }

    public async Task InstallBotAsync(InstallEvent installEvent)
    {
        var organization = await _organizationRepository.InstallBotAsync(installEvent);

        // If the organization is still eligible for a trial, then activate the trial.
        // If we're in an environment where orgs get Unlimited by default (dev/canary), they won't be eligible for a trial.
        if (organization is { TrialEligible: true, PlanType: PlanType.Free })
        {
            await _organizationRepository.StartTrialAsync(
                organization,
                new TrialPlan(PlanType.Business, _clock.UtcNow + TrialPlan.TrialLength));

            organization.Settings = (organization.Settings ?? new OrganizationSettings())
                with
            {
                // AI services are enabled by default for new organizations when they start a free trial.
                AIEnhancementsEnabled = true
            };
            await _organizationRepository.SaveChangesAsync();
        }

        var (slackAppIntegration, _) = await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);
        if (slackAppIntegration?.ExternalId == organization.BotAppId)
        {
            // TODO: Replace this with installer Member, if this doesn't go away altogether
            var abbot = await _organizationRepository.EnsureAbbotMember(organization);
            await _integrationRepository.EnableAsync(organization, IntegrationType.SlackApp, abbot);
        }

        await _publishEndpoint.Publish(new OrganizationUpdated()
        {
            OrganizationId = organization,
        });
    }

    public async Task UninstallBotAsync(IPlatformEvent uninstallEvent)
    {
        InstallationInfo? uninstallInfo = null;

        var uninstall = uninstallEvent.Payload.Require<UninstallPayload>();
        var organization = uninstallEvent.Organization;

        _logger.MethodEntered(typeof(BotInstaller), nameof(UninstallBotAsync),
            $"Installing App {uninstall.BotAppId} to Organization {organization.Id} ({organization.PlatformId})...");

        var orgAuth = new SlackAuthorization(organization);
        if (uninstall.BotAppId == organization.BotAppId
            // Actually installed?
            && orgAuth.BotUserId is not null)
        {
            uninstallInfo = InstallationInfo.Create(InstallationEventAction.Uninstall, organization);

            // Leave BotAppId/BotAppName intact so we know which app to Reinstall
            organization.PlatformBotId = null;
            organization.PlatformBotUserId = null;
            organization.BotName = null;
            organization.BotAvatar = null;
            organization.ApiToken = null;
            organization.Scopes = null;
        }

        var (integration, slackApp) =
            await _integrationRepository.GetIntegrationAsync<SlackAppSettings>(organization);
        if (integration is not null && slackApp is not null)
        {
            if (uninstall.BotAppId == slackApp.Authorization?.AppId)
            {
                uninstallInfo ??= InstallationInfo.Create(InstallationEventAction.Uninstall, slackApp.Authorization);
                await _integrationRepository.SaveSettingsAsync(integration,
                    slackApp with { Authorization = null });
            }

            if (uninstall.BotAppId == slackApp.DefaultAuthorization?.AppId)
            {
                uninstallInfo ??= InstallationInfo.Create(InstallationEventAction.Uninstall, slackApp.DefaultAuthorization);
                await _integrationRepository.SaveSettingsAsync(integration,
                    slackApp with { DefaultAuthorization = null });
            }
        }

        if (uninstallInfo is not null)
        {
            organization.LastPlatformUpdate = _clock.UtcNow;
            await _organizationRepository.SaveChangesAsync();

            var actor = uninstallEvent.From;
            await _auditLog.LogUninstalledAbbotAsync(uninstallInfo, organization, actor);

            _logger.AbbotUninstalled(
                uninstallInfo.AppName,
                uninstallInfo.AppId,
                organization.Name,
                organization.Id,
                organization.PlatformId);
        }

        await _publishEndpoint.Publish(new OrganizationUpdated()
        {
            OrganizationId = organization,
        });
    }
}

static partial class BotInstallerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message =
            "Installed {BotAppName} ({BotAppId}) to organization (Name: {OrganizationName}, Id: {OrganizationId}, PlatformId: {PlatformId})")]
    public static partial void AbbotInstalled(
        this ILogger<BotInstaller> logger,
        string? botAppName,
        string? botAppId,
        string? organizationName,
        int organizationId,
        string platformId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message =
            "Uninstalled {BotAppName} ({BotAppId}) from organization (Name: {OrganizationName}, Id: {OrganizationId}, PlatformId: {PlatformId})")]
    public static partial void AbbotUninstalled(
        this ILogger<BotInstaller> logger,
        string? botAppName,
        string? botAppId,
        string? organizationName,
        int organizationId,
        string platformId);
}

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

public class IntegrationRepository : IIntegrationRepository
{
    readonly AbbotContext _db;
    readonly IAnalyticsClient _analyticsClient;
    readonly IAuditLog _auditLog;

    public IntegrationRepository(
        AbbotContext db,
        IAnalyticsClient analyticsClient,
        IAuditLog auditLog)
    {
        _db = db;
        _analyticsClient = analyticsClient;
        _auditLog = auditLog;
    }

    public async Task<Integration> CreateIntegrationAsync(Organization organization, IntegrationType type, bool enabled = false)
    {
        var integration = new Integration
        {
            Enabled = enabled,
            Organization = organization,
            Type = type,
            Settings = "{}",
        };
        await _db.Integrations.AddAsync(integration);
        await _db.SaveChangesAsync();

        return integration;
    }

    public async Task<Integration> EnsureIntegrationAsync(Organization organization, IntegrationType type, bool enabled = false)
    {
        return await GetIntegrationAsync(organization, type)
            ?? await CreateIntegrationAsync(organization, type, enabled);
    }

    public async Task<Integration> EnableAsync(Organization organization, IntegrationType type, Member actor)
    {
        VerifyIntegrationType(type, nameof(type));

        var integration = await EnsureIntegrationAsync(organization, type);
        if (integration is { Enabled: true })
        {
            // Already enabled, no need to track audit/analytics events.
            return integration;
        }

        integration.Enabled = true;
        await _db.SaveChangesAsync();

        await _auditLog.LogIntegrationStatusChangedAsync(integration, actor);
        _analyticsClient.Track(
            "Integration Enabled",
            AnalyticsFeature.Integrations,
            actor,
            organization,
            new()
            {
                { "integration", type.ToString() },
            });
        return integration;
    }

    public async Task DisableAsync(Organization organization, IntegrationType type, Member actor)
    {
        VerifyIntegrationType(type, nameof(type));

        var existing = await GetIntegrationAsync(organization, type);
        // "not Enabled: true" means we catch both null and 'Enabled: false' cases.
        if (existing is not { Enabled: true })
        {
            // It's not enabled, so no need to do the rest.
            return;
        }

        existing.Enabled = false;
        await _db.SaveChangesAsync();

        await _auditLog.LogIntegrationStatusChangedAsync(existing, actor);
        _analyticsClient.Track(
            "Integration Disabled",
            AnalyticsFeature.Integrations,
            actor,
            organization,
            new()
            {
                { "integration", type.ToString() },
            });
    }

    public async ValueTask<Integration?> GetIntegrationAsync(Organization organization, IntegrationType type)
    {
        var integrations = await GetIntegrationsAsync(organization);
        var integration = integrations.SingleOrDefault(i => i.Type == type);
        return integration;
    }

    public async ValueTask<(Integration?, TSettings?)> GetIntegrationAsync<TSettings>(string externalId)
        where TSettings : class, IIntegrationSettings
    {
        var integrationType = TSettings.IntegrationType;
        var integration = await _db.Integrations
            .Include(i => i.Organization)
            .SingleOrDefaultAsync(i => i.ExternalId == externalId && i.Type == integrationType);

        return integration is not null
            ? (integration, ReadSettings<TSettings>(integration))
            : (null, null);
    }

    public async ValueTask<(Integration?, TSettings?)> GetIntegrationAsync<TSettings>(Organization organization)
        where TSettings : class, IIntegrationSettings =>
        await GetIntegrationAsync(organization, TSettings.IntegrationType) is { } integration
            ? (integration, ReadSettings<TSettings>(integration))
            : (null, null);

    public async Task<Integration?> GetIntegrationByIdAsync(int integrationId, CancellationToken cancellationToken)
    {
        return await _db.Integrations
            .Include(i => i.Organization)
            .SingleOrDefaultAsync(i => i.Id == integrationId, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<Integration>> GetIntegrationsAsync(Organization organization)
    {
        // We can't rely on `organizations.Integrations` being null to determine if the collection is loaded.
        // Also, LoadAsync() already checks `IsLoaded`. See: https://haacked.com/archive/2022/09/30/ef-core-collection-pitfalls/
        await _db.Entry(organization)
            .Collection(o => o.Integrations)
            .LoadAsync();

        return organization.Integrations;
    }

    public async Task<TicketingIntegration?> GetTicketingIntegrationByIdAsync(Organization organization, Id<Integration> id)
    {
        var integration = await _db.Integrations.FindByIdAsync(id);
        return integration?.OrganizationId == organization.Id
            && TryGetTicketingSettings(integration, out var settings)
            ? new(integration, settings)
            : null;
    }

    public async Task<IReadOnlyList<TicketingIntegration>> GetTicketingIntegrationsAsync(Organization organization)
    {
        return Filter(await GetIntegrationsAsync(organization)).ToList();

        IEnumerable<TicketingIntegration> Filter(IEnumerable<Integration> integrations)
        {
            foreach (var integration in integrations)
            {
                if (TryGetTicketingSettings(integration, out var settings))
                {
                    yield return new(integration, settings);
                }
            }
        }
    }

    public virtual bool TryGetTicketingSettings(Integration integration, [NotNullWhen(true)] out ITicketingSettings? settings)
    {
        switch (integration.Type)
        {
            case IntegrationType.Zendesk:
                settings = ReadSettings<ZendeskSettings>(integration);
                return true;
            case IntegrationType.HubSpot:
                settings = (ReadSettings<HubSpotSettings>(integration));
                return true;
            case IntegrationType.GitHub:
                settings = (ReadSettings<GitHubSettings>(integration));
                return true;
            case IntegrationType.Ticketing:
                settings = (ReadSettings<TicketingSettings>(integration));
                return true;
        }

        settings = default;
        return false;
    }

    public T ReadSettings<T>(Integration integration)
        where T : class, IIntegrationSettings
    {
        return JsonConvert.DeserializeObject<T>(integration.Settings).Require();
    }

    public async Task<T> SaveSettingsAsync<T>(Integration integration, T settings)
        where T : class, IIntegrationSettings
    {
        integration.Settings = JsonConvert.SerializeObject(settings);
        await _db.SaveChangesAsync();
        return settings;
    }

    static void VerifyIntegrationType(IntegrationType integrationType, string paramName)
    {
        switch (integrationType)
        {
            case IntegrationType.Zendesk:
            case IntegrationType.SlackApp:
            case IntegrationType.HubSpot:
            case IntegrationType.GitHub:
            case IntegrationType.Ticketing:
                return;

            default:
                throw new ArgumentOutOfRangeException(paramName, $"Invalid Integration Type: {integrationType}");
        }
    }
}

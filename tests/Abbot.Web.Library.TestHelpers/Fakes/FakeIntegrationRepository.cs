using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.DataProtection;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeIntegrationRepository : IntegrationRepository
{
    readonly Dictionary<IntegrationType, Func<Integration, ITicketingSettings>> _parsers = new();

    public FakeIntegrationRepository(
        AbbotContext db,
        IAnalyticsClient analyticsClient,
        IAuditLog auditLog)
        : base(db, analyticsClient, auditLog)
    {
    }

    public void AddSettingsType<TSettings>()
        where TSettings : class, IIntegrationSettings, ITicketingSettings
    {
        _parsers.Add(TSettings.IntegrationType, ReadSettings<TSettings>);
    }

    public override bool TryGetTicketingSettings(
        Integration integration,
        [NotNullWhen(true)] out ITicketingSettings? settings)
    {
        if (_parsers.TryGetValue(integration.Type, out var parser))
        {
            settings = parser(integration);
            return true;
        }

        return base.TryGetTicketingSettings(integration, out settings);
    }
}

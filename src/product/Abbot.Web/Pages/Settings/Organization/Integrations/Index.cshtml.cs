using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations;

public class IndexPage : UserPage
{
    readonly IIntegrationRepository _integrationRepository;

#pragma warning disable CA1721
    public IReadOnlyList<Integration> Integrations { get; set; } = null!;
#pragma warning restore CA1721

    public IEnumerable<(Integration, TSettings)> GetIntegrations<TSettings>()
        where TSettings : class, IIntegrationSettings =>
        Integrations
            .Where(i => i.Type == TSettings.IntegrationType)
            .Select(i => (i, _integrationRepository.ReadSettings<TSettings>(i)));

    public bool Enabled(IntegrationType type) => Integrations.SingleOrDefault(i => i.Type == type)?.Enabled == true;

    public IndexPage(IIntegrationRepository integrationRepository)
    {
        _integrationRepository = integrationRepository;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Integrations", new { Id = Organization.PlatformId });

    public async Task<IActionResult> OnGetAsync()
    {
        Integrations = await _integrationRepository.GetIntegrationsAsync(Organization);

        return Page();
    }
}

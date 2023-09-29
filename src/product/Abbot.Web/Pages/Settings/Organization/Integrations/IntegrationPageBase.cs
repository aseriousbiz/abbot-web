using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations;

public abstract class IntegrationPageBase<TSettings> : UserPage
    where TSettings : class, IIntegrationSettings
{
    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Integrations", new { Id = Organization.PlatformId });

    public IntegrationType IntegrationType => TSettings.IntegrationType;

    public IIntegrationRepository IntegrationRepository { get; }

    public Integration Integration { get; set; } = null!;

    public TSettings Settings { get; set; } = default!;

    [MemberNotNullWhen(true, nameof(Integration))]
    public virtual bool IsEnabled => Integration?.Enabled == true;

    protected IntegrationPageBase(IIntegrationRepository integrationRepository)
    {
        IntegrationRepository = integrationRepository;
    }
}

public abstract class SingleIntegrationPageBase<TSettings> : IntegrationPageBase<TSettings>
    where TSettings : class, IIntegrationSettings
{
    protected SingleIntegrationPageBase(IIntegrationRepository integrationRepository)
        : base(integrationRepository)
    {
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        // Run the base handler with us as the "next" handler.
        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                // Then, run our code and chain to the next one.
                Integration = await IntegrationRepository.EnsureIntegrationAsync(Organization, IntegrationType);
                Settings = IntegrationRepository.ReadSettings<TSettings>(Integration);
                return await next();
            });
    }
}

public abstract class MultipleIntegrationPageBase<TSettings> : IntegrationPageBase<TSettings>
    where TSettings : class, IIntegrationSettings
{
    protected MultipleIntegrationPageBase(IIntegrationRepository integrationRepository)
        : base(integrationRepository)
    {
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        // Run the base handler with us as the "next" handler.
        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                // Then, run our code and chain to the next one.
                if (!context.RouteData.Values.TryGetValue("id", out var id)
                    || !int.TryParse(id?.ToString(), out var integrationId)
                    || await IntegrationRepository.GetIntegrationByIdAsync(integrationId) is not { } integration
                    || integration.OrganizationId != Organization.Id
                    || integration.Type != TSettings.IntegrationType)
                {
                    context.Result = NotFound();
                    return null!;
                }

                Integration = integration;
                Settings = IntegrationRepository.ReadSettings<TSettings>(Integration);
                return await next();
            });
    }
}

using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Eventing.Entities;

/// <summary>
/// Messages implementing <see cref="IOrganizationMessage"/> are discarded
/// unless <see cref="OrganizationFilter{T}"/> can resolve an enabled <see cref="Organization"/>,
/// which is available in consumers as <c>context.GetPayload&lt;Organization&gt;()</c>.
/// </summary>
// This isn't for polymorphic publishing, so exclude it from the topology.
[ExcludeFromTopology]
public interface IOrganizationMessage
{
    Id<Organization> OrganizationId { get; }
}

/// <summary>
/// Adds an <see cref="Organization"/> payload for messages implementing <see cref="IOrganizationMessage"/>.
/// Messages for missing/disabled Organizations are discarded.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public class OrganizationFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    readonly IOrganizationRepository _organizationRepository;
    readonly ILogger<OrganizationFilter<T>> _logger;

    public OrganizationFilter(IOrganizationRepository organizationRepository, ILogger<OrganizationFilter<T>> logger)
    {
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope(nameof(OrganizationFilter<T>));
    }

    public async Task Send(
        ConsumeContext<T> context,
        IPipe<ConsumeContext<T>> next)
    {
        if (context is not ConsumeContext<IOrganizationMessage> { Message: { } orgMessage })
        {
            await next.Send(context);
            return;
        }

        var organizationId = orgMessage.OrganizationId;
        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            _logger.EntityNotFound(organizationId);
            return;
        }

        using var _ = _logger.BeginOrganizationScope(organization);
        if (!organization.Enabled)
        {
            _logger.OrganizationDisabled();
            return;
        }

        context.AddOrUpdatePayload(() => organization, _ => organization);
        await next.Send(context);
    }
}

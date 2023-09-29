using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Eventing.Entities;

/// <summary>
/// Messages implementing <see cref="IPlaybookRunMessage"/> are discarded
/// unless <see cref="PlaybookRunFilter{T}"/> can resolve an valid <see cref="PlaybookRun"/>,
/// which is available in consumers as <c>context.GetPayload&lt;PlaybookRun&gt;()</c>.
/// </summary>
// This isn't for polymorphic publishing, so exclude it from the topology.
[ExcludeFromTopology]
public interface IPlaybookRunMessage : CorrelatedBy<Guid>
{
    Guid PlaybookRunId { get; }
#pragma warning disable CA1033
    Guid CorrelatedBy<Guid>.CorrelationId => PlaybookRunId;
#pragma warning restore CA1033
}

/// <summary>
/// Messages implementing <see cref="IPlaybookRunGroupMessage"/> are discarded
/// unless <see cref="PlaybookRunFilter{T}"/> can resolve an valid <see cref="PlaybookRunGroup"/>,
/// which is available in consumers as <c>context.GetPayload&lt;PlaybookRunGroup&gt;()</c>.
/// </summary>
// This isn't for polymorphic publishing, so exclude it from the topology.
[ExcludeFromTopology]
public interface IPlaybookRunGroupMessage : CorrelatedBy<Guid>
{
    Guid PlaybookRunGroupId { get; }
#pragma warning disable CA1033
    Guid CorrelatedBy<Guid>.CorrelationId => PlaybookRunGroupId;
#pragma warning restore CA1033
}

/// <summary>
/// Adds an <see cref="PlaybookRun"/> payload for messages implementing <see cref="IPlaybookRunMessage"/> or <see cref="IPlaybookRunGroupMessage"/>.
/// Messages for missing/disabled PlaybookRuns or PlaybookRunGroups are discarded.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public class PlaybookRunFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    readonly PlaybookRepository _playbookRepository;
    readonly ILogger<PlaybookRunFilter<T>> _logger;

    public PlaybookRunFilter(PlaybookRepository playbookRepository, ILogger<PlaybookRunFilter<T>> logger)
    {
        _playbookRepository = playbookRepository;
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope(nameof(PlaybookRunFilter<T>));
    }

    public async Task Send(
        ConsumeContext<T> context,
        IPipe<ConsumeContext<T>> next)
    {
        if (context is ConsumeContext<IPlaybookRunMessage> { Message: { } runMessage })
        {
            var run = await _playbookRepository.GetRunAsync(runMessage.PlaybookRunId);
            if (run is null)
            {
                _logger.EntityNotFound(runMessage.PlaybookRunId, typeof(PlaybookRun));
                return;
            }

            using var orgScope = _logger.BeginOrganizationScope(run.Playbook.Organization);
            using var playbookScope = _logger.BeginPlaybookScope(run.Playbook);
            using var groupScope = run.Group is not null
                ? _logger.BeginPlaybookRunGroupScope(run.Group)
                : null;

            using var runScope = _logger.BeginPlaybookRunScope(run);

            context.AddOrUpdatePayload(() => run, _ => run);
        }
        else if (context is ConsumeContext<IPlaybookRunGroupMessage> { Message: { } groupMessage })
        {
            var group = await _playbookRepository.GetRunGroupAsync(groupMessage.PlaybookRunGroupId);
            if (group is null)
            {
                _logger.EntityNotFound(groupMessage.PlaybookRunGroupId, typeof(PlaybookRunGroup));
                return;
            }

            using var orgScope = _logger.BeginOrganizationScope(group.Playbook.Organization);
            using var playbookScope = _logger.BeginPlaybookScope(group.Playbook);
            using var groupScope = _logger.BeginPlaybookRunGroupScope(group);

            context.AddOrUpdatePayload(() => group, _ => group);
        }
        await next.Send(context);
    }
}

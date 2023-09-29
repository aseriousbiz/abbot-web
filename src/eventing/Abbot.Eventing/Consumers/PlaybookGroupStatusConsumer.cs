using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Eventing.Consumers;

/// <summary>
/// Consumer for Playbook state change events.
/// </summary>
public class PlaybookGroupStatusConsumer : IConsumer<PlaybookRunInGroupComplete>
{
    readonly PlaybookRepository _playbookRepository;
    readonly IUrlGenerator _urlGenerator;
    readonly IAuditLog _auditLog;
    readonly IOrganizationRepository _organizationRepository;
    readonly ILogger<PlaybookGroupStatusConsumer> _logger;

    public class Definition : AbbotConsumerDefinition<PlaybookGroupStatusConsumer>
    {
        public Definition()
        {
            RequireSession("playbook-group-status");
        }
    }

    public PlaybookGroupStatusConsumer(PlaybookRepository playbookRepository, IUrlGenerator urlGenerator, IAuditLog auditLog, IOrganizationRepository organizationRepository, ILogger<PlaybookGroupStatusConsumer> logger)
    {
        _playbookRepository = playbookRepository;
        _urlGenerator = urlGenerator;
        _auditLog = auditLog;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PlaybookRunInGroupComplete> context)
    {
        var group = context.GetPayload<PlaybookRunGroup>();
        var run = await _playbookRepository.GetRunAsync(context.Message.PlaybookRunId);
        if (run is null)
        {
            _logger.EntityNotFound(context.Message.PlaybookRunId, typeof(PlaybookRun));
            return;
        }

        using var runScope = _logger.BeginPlaybookRunScope(run);

        var organization = run.Playbook.Organization;
        var result = run.Properties.Result;
        if (result is null)
        {
            return;
        }

        var abbot = await _organizationRepository.EnsureAbbotMember(run.Playbook.Organization);
        await _auditLog.LogAuditEventAsync(
            new()
            {
                ParentIdentifier = run.Properties.RootAuditEventId,
                IsTopLevel = false,
                Type = new("Playbook.Run", "Completed"),
                Description =
                    $"Run of playbook `{run.Playbook.Name}` completed with outcome '{result.Outcome}'",
                Details =
                    $"Run `{run.CorrelationId}` of playbook `{run.Playbook.Name}` completed with outcome '{result.Outcome}'",
                Actor = abbot,
                Organization = organization,
                EntityId = run.Id,
                Properties = PlaybookRepository.PlaybookRunLogProperties.FromPlaybookRun(run, null),
            },
            new(AnalyticsFeature.Playbooks, "Playbook completed")
            {
                ["outcome"] = result.Outcome.ToString(),
                ["problem"] = run.Properties.Result?.Problem?.Type,
            });

        // Update the group's run counts.
        // This should be safe because in Azure Service Bus we run under a message session defined by the group ID.
        var runCounts = group.Properties.RunCountsByOutcome;
        var oldCount = runCounts.TryGetValue(result.Outcome, out var v)
            ? v
            : 0;

        runCounts[result.Outcome] = oldCount + 1;
        await _playbookRepository.UpdateRunGroupAsync(group, abbot);

        // If this is the first failure, send a notification.
        if (oldCount == 0 && result.Outcome == PlaybookRunOutcome.Faulted)
        {
            await context.Publish(new PublishRoomNotification
            {
                OrganizationId = organization,
                RoomId = null, // TODO: Resolve room from inputs or outputs?
                Notification =
                    new("❌", $"Playbook '{group.Playbook.Name}' Failed",
                        $"<{_urlGenerator.PlaybookRunGroupPage(group)}|View details>"),
            });
            _logger.PublishedFailureNotificationForGroup();
        }
    }
}

public static partial class PlaybookGroupStatusConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Published failure notification for group")]
    public static partial void PublishedFailureNotificationForGroup(this ILogger<PlaybookGroupStatusConsumer> logger);
}

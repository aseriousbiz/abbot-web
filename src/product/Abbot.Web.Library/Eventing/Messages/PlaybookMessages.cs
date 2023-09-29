using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Eventing.Messages;

public record ExecutePlaybook(
    Guid PlaybookRunId,
    string PlaybookTriggerId,
    Id<PlaybookVersion> PlaybookVersionId,
    Id<Playbook> PlaybookId,
    Id<Organization> OrganizationId) : IPlaybookRunMessage;

public record CancelPlaybook(Guid PlaybookRunId, Id<Member> Canceller) : IPlaybookRunMessage;

public record RunPlaybookStep(
    Guid PlaybookRunId,
    ActionReference Step) : IPlaybookRunMessage;

public record ResumeSuspendedStep(
    Guid PlaybookRunId,
    ActionReference Step) : IPlaybookRunMessage
{
    public IDictionary<string, object?> SuspendState { get; init; } = new Dictionary<string, object?>();
}

public record CancelSuspendedStep(
    Guid PlaybookRunId,
    ActionReference Step) : IPlaybookRunMessage;

public record PlaybookStepComplete(
    Guid PlaybookRunId,
    ActionReference Step,
    StepResult Result) : IPlaybookRunMessage;

public record PlaybookRunComplete(
    Guid PlaybookRunId) : IPlaybookRunMessage;

public record PlaybookRunInGroupComplete(
    Guid PlaybookRunGroupId,
    Guid PlaybookRunId) : IPlaybookRunGroupMessage;

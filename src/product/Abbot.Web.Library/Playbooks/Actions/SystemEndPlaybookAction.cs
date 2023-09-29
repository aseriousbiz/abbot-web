namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// Completes the playbook. Any steps after this one are ignored.
/// </summary>
public class SystemCompletePlaybookAction : ActionType<SystemCompletePlaybookAction.Executor>
{
    public override StepType Type { get; } = new("system.complete-playbook", StepKind.Action)
    {
        Category = "system",
        Presentation = new()
        {
            Label = "Complete Playbook",
            Icon = "fa-flag-checkered",
            Description = "Completes this Playbook run. Any steps after this are ignored.",
        },
    };

    public class Executor : IActionExecutor
    {
        public Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            return Task.FromResult(new StepResult(StepOutcome.CompletePlaybook));
        }
    }
}

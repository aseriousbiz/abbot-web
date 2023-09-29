using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Playbooks.Actions;

public class SystemHttpGetAction : ActionType<SystemHttpGetAction.Executor>
{
    public override StepType Type { get; } = new("http.get", StepKind.Action)
    {
        Category = "http",
        Presentation = new()
        {
            Label = "Fetch URL",
            Icon = "fa-file-arrow-down",
            Description = "Performs an HTTP GET to a URL",
        },
        RequiredFeatureFlags =
        {
            FeatureFlags.PlaybookStepsWave1,
        }
    };

    public class Executor : IActionExecutor
    {
        public Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            return Task.FromResult(new StepResult(StepOutcome.Failed)
            {
                Problem = Problems.NotImplemented($"Step {context.Step.Type} not yet implemented"),
            });
        }
    }
}

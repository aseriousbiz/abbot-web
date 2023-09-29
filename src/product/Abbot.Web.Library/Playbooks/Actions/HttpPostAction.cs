using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Playbooks.Actions;

public class SystemHttpPostAction : ActionType<SystemHttpPostAction.Executor>
{
    public override StepType Type { get; } = new("http.post", StepKind.Action)
    {
        Category = "http",
        Presentation = new()
        {
            Label = "Post to URL",
            Icon = "fa-file-arrow-up",
            Description = "Performs an HTTP POST to a URL",
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

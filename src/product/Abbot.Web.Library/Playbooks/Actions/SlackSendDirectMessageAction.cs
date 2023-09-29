using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Playbooks.Actions;

public class SlackSendDirectMessageAction : ActionType<SlackSendDirectMessageAction.Executor>
{
    public override StepType Type { get; } = new("slack.send-direct-message", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Send Direct Message",
            Icon = "fa-paper-plane-top",
            Description = "Sends a direct message to specific people",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
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

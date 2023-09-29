using System.Collections.Generic;

namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// An action that checks a condition and if the condition is true, continues the playbook. If the condition is false,
/// the playbook is completed successfully.
/// </summary>
public class SystemContinueIfAction : ActionType<SystemContinueIfAction.Executor>
{
    public override StepType Type { get; } = new("system.continue-if", StepKind.Action)
    {
        Category = "system",
        Presentation = new()
        {
            Label = "Continue If",
            Icon = "fa-arrow-progress",
            Description = "Runs the next step if a certain condition is met",
        },
        Inputs =
        {
            new("left", "Condition", PropertyType.PredefinedExpression)
            {
                Description = "The left side of the comparison expression.",
                Required = true,
            },
            new("comparison", "Comparison", PropertyType.ComparisonType)
            {
                Description = "The operator to use to compare left with right.",
                Default = "StartsWith",
                Required = true,
            },
            new("right", "", PropertyType.Comparand)
            {
                Description = "The right side of the comparison expression.",
                Required = true,
            },
        },
    };

    public class Executor : IActionExecutor
    {
        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var isMatch = ActionHelpers.EvaluateCondition(context);

            var (title, details) = isMatch
                ? ("Condition was true", "Continuing to the next step.")
                : ("Condition was false", "Ending the Playbook run.");

            return new StepResult(isMatch ? StepOutcome.Succeeded : StepOutcome.CompletePlaybook)
            {
                Notices = new List<Notice>
                {
                    new Notice(NoticeType.Information, title, details),
                }
            };
        }
    }
}

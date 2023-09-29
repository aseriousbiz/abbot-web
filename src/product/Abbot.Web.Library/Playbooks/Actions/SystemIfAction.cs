using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Playbooks.Actions;

public class SystemIfAction : ActionType<SystemIfAction.Executor>
{
    const string TrueBranchName = "true";
    const string FalseBranchName = "false";

    public override StepType Type { get; } = new("system.if", StepKind.Action)
    {
        Category = "system",
        Presentation = new()
        {
            Label = "If",
            Icon = "fa-code-fork fa-rotate-180",
            Description = "Branches the Playbook based on a condition",
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
        Branches =
        {
            new(TrueBranchName, "If true, run these steps"),
            new(FalseBranchName, "If false, run these steps"),
        }
    };

    public class Executor : IActionExecutor
    {
        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var isMatch = ActionHelpers.EvaluateCondition(context);

            // "Null" from isMatch indicates 'left' is empty
            // But 'null' can never satisfy any provided condition so it's equivalent to false.
            var (branch, description) = isMatch
                ? (TrueBranchName, "Condition was true")
                : (FalseBranchName, "Condition was false");

            var sequence = context.Step.Branches.TryGetValue(branch, out var s)
                ? s
                : null;

            return new StepResult(StepOutcome.Succeeded)
            {
                CallBranch = new(branch, sequence, description),
            };
        }
    }
}

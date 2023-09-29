using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Actions;

public class StaffOutputAction : ActionType<StaffOutputAction.Executor>
{
    public override StepType Type { get; } = new("staff.output", StepKind.Action)
    {
        Category = "staff",
        StaffOnly = true,
        Presentation = new()
        {
            Label = "Output",
            Icon = "fa-pen",
            Description = "Produces an output property for testing",
        },
        Inputs =
        {
            new("name", "Name", PropertyType.String)
            {
                Description = "The name of the output property to produce",
                Placeholder = "test_output",
            },
            new("value", "Value", PropertyType.String)
            {
                Description = "The value of the output property to produce",
                Placeholder = "Hello, world!",
            },
        },
    };


    public class Executor : IActionExecutor
    {
        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var name = context.Expect<string>("name");
            var value = context.Expect<string>("value");
            return new(StepOutcome.Succeeded)
            {
                Outputs =
                {
                    [name] = value,
                },
            };
        }
    }
}

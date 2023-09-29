namespace Serious.Abbot.Playbooks.Actions;

public class StaffCrashAction : ActionType<StaffCrashAction.Executor>
{
    public override StepType Type { get; } = new("staff.crash", StepKind.Action)
    {
        Category = "staff",
        StaffOnly = true,
        Presentation = new()
        {
            Label = "Crash",
            Icon = "fa-hammer-crash",
            Description = "Fails immediately with the provided message",
        },
        Inputs =
        {
            new("text", "Message", PropertyType.String)
            {
                Description = "The message of the exception",
                Placeholder = "Kaboom!",
                Required = true,
            },
        },
    };

    public class Executor : IActionExecutor
    {
        public Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var message = context.Get<string>("text", "message") ?? "Kaboom";
#pragma warning disable CA2201
            throw new Exception(message);
#pragma warning restore CA2201
        }
    }
}

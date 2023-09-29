using Microsoft.Extensions.Logging;

namespace Serious.Abbot.Playbooks.Actions;

#pragma warning disable CA1848
public class StaffSlowAction : ActionType<StaffSlowAction.Executor>
{
    static readonly TimeSpan MaximumWait = TimeSpan.FromMinutes(5);

    public override StepType Type { get; } = new("staff.slow", StepKind.Action)
    {
        Category = "staff",
        StaffOnly = true,
        Presentation = new()
        {
            Label = "Slow",
            Icon = "fa-timer",
            Description = "Waits without suspending for the provided number of seconds, then succeeds",
        },
        Inputs =
        {
            new("wait_for", "Wait for Seconds", PropertyType.Integer)
            {
                Description = "The number of seconds to wait.",
                Required = true,
            },
        },
    };

    public class Executor : IActionExecutor
    {
        readonly ILogger<Executor> _logger;

        public Executor(ILogger<Executor> logger) => _logger = logger;

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var waitForSeconds = context.Expect<long>("wait_for");
            var waitFor = TimeSpan.FromSeconds(waitForSeconds);
            if (waitFor > MaximumWait)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.ArgumentError("wait_for",
                        $"Cannot wait for more than {MaximumWait.TotalSeconds}")
                };
            }
            _logger.LogInformation("Waiting for {WaitForSeconds} seconds", waitForSeconds);
            await Task.Delay(waitFor);
            _logger.LogInformation("Wait Complete");

            return new StepResult(StepOutcome.Succeeded);
        }
    }
}

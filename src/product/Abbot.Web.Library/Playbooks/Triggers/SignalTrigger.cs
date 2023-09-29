namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// A trigger that is fired when a custom signal is received.
/// </summary>
public class SignalTrigger : ITriggerType
{
    public const string Id = "system.signal";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "system",
        StaffOnly = true,
        Presentation = new StepPresentation
        {
            Label = "Signal",
            Icon = "fa-signal-stream",
            Description = "Runs when a Signal is raised from a Skill",
        },
        Inputs =
        {
            // The name of the signal.
            new("signal", "Signal", PropertyType.Signal)
        },
        Outputs =
        {
            new("channel", "Channel", PropertyType.Channel) {},
            new("customer", "Customer", PropertyType.Customer),
            new("arguments", "Arguments", PropertyType.String),
            new("signal", "Signal", PropertyType.Signal),
        }
    };
}

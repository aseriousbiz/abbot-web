namespace Serious.Abbot.Playbooks.Triggers;

public class ManualTrigger : ITriggerType
{
    public const string Id = "abbot.manual";
    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "abbot",
        Presentation = new StepPresentation
        {
            Label = "Run Playbook Button",
            Icon = "fa-circle-play",
            Description = "Add a Run Playbook button to the Playbook list page to run this playbook manually.",
        },
        AdditionalDispatchTypes =
        {
            DispatchType.ByCustomer,
        }
    };
}

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers a playbook based on a schedule.
/// </summary>
public class ScheduleTrigger : ITriggerType
{
    public const string Id = "system.schedule";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "system",
        Presentation = new StepPresentation
        {
            Label = "Schedule",
            Icon = "fa-calendar",
            Description = "Runs on the specified schedule",
        },
        Inputs =
        {
            new("schedule", "Schedule", PropertyType.Schedule)
            {
                Default = new DailySchedule(12, 0),
                Description = "The schedule to run on",
            },
            new("tz", "Timezone", PropertyType.Timezone)
            {
                Description = "The timezone to use for the schedule",
            },
        },
        AdditionalDispatchTypes =
        {
            DispatchType.ByCustomer,
        }
    };
}

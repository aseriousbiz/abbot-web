namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers playbooks when a customer is created.
/// </summary>
public class CustomerCreatedTrigger : ITriggerType
{
    public const string Id = "abbot.customer-created";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "abbot",
        Presentation = new StepPresentation
        {
            Label = "New Customer Created",
            Icon = "fa-user-plus",
            Description = "Runs when a new customer record is created in Abbot",
        },
        Outputs =
        {
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer that was created",
                ExpressionContext = "that was created",
            },
            // A customer can have more than one channel.
            new("channels", "Channels", PropertyType.Channels)
            {
                Description = "All channels associated with the created customer"
            },
            // However, other actions expect a single channel, so we'll pass the first one here, if any.
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The first channel associated with the created customer, if any",
            },
        }
    };
}

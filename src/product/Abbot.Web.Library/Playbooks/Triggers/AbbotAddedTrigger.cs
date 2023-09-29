using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers playbooks when Abbot is added to a channel.
/// </summary>
public class AbbotAddedTrigger : ITriggerType
{
    public const string Id = "slack.abbot-added-to-channel";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "slack",
        Presentation = new StepPresentation
        {
            Label = "Abbot Added to Channel",
            Icon = "fa-message-bot",
            Description = "Runs when Abbot joins a channel",
        },
        Inputs =
        {
            new("segments", "Customer segments", PropertyType.CustomerSegments)
            {
                Description = "Only trigger for customers in these segments",
            },
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Outputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel Abbot was added to",
                ExpressionContext = "where Abbot was added",
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer associated with the channel Abbot was added to",
                ExpressionContext = "with the channel where Abbot was added",
            },
        }
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
        => triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out reason);
}

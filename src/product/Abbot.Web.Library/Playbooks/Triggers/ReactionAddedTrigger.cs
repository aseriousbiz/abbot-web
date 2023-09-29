using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Trigger that runs when a reaction is added to a message.
/// </summary>
public class ReactionAddedTrigger : ITriggerType
{
    public const string Id = "slack.reaction-added";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "slack",
        Presentation = new StepPresentation
        {
            Label = "Reaction Added",
            Icon = "fa-thumbs-up",
            Description = "Runs when a reaction is added to a message and matches one of the reactions in this list.",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("reaction", "Reactions", PropertyType.Emoji),
            new("segments", "Customer segments", PropertyType.CustomerSegments)
            {
                Description = "Only trigger for customers in these segments",
            },
        },
        Outputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel the reaction was added in"
            },
            new("conversation", "Conversation", PropertyType.Conversation)
            {
                Description = "The conversation the reaction was added in if any",
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer associated with the channel the reaction was added in"
            },
            new("message", "Message", PropertyType.Message)
            {
                Description = "The message the reaction was added to"
            },
            new("reaction", "Reaction", PropertyType.Emoji)
            {
                Description = "The name of the reaction that was added"
            },
        }
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
    {
        if (!triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out var segmentReason))
        {
            reason = segmentReason;
            return false;
        }

        if (!triggerStep.InputValuesForKeyMatchOutputValue<string, string>(
            "reaction",
            reaction => reaction,
            inputOptional: false,
            outputs,
            "reaction",
            r => new[] { r },
            out var reactionReason))
        {
            reason = reactionReason;
            return false;
        }

        reason = $"{segmentReason} {reactionReason}";
        return true;
    }
}

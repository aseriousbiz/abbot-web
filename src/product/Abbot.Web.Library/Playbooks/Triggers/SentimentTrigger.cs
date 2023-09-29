using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Triggers;

public class SentimentTrigger : ITriggerType
{
    public const string Id = "abbot.conversation-sentiment";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "ai",
        Presentation = new StepPresentation
        {
            Label = "Negative Sentiment",
            Icon = "fa-thumbs-down",
            Description = "Runs when AI classifies a conversation as having negative sentiment",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("sentiment", "Sentiment", PropertyType.String) { Hidden = true, Default = "negative" },
            new("segments", "Customer segments", PropertyType.CustomerSegments)
            {
                Description = "Only trigger for customers in these segments",
            },
        },
        Outputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel with the negative sentiment"
            },
            new("conversation", "Conversation", PropertyType.Conversation)
            {
                Description = "The conversation with the negative sentiment"
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer with the negative sentiment"
            },
            new("message", "Message", PropertyType.Message)
            {
                Description = "The message with the negative sentiment"
            },
            new("sentiment", "Sentiment", PropertyType.String)
            {
                Description = "The sentiment that was detected"
            },
        },
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
    {
        if (!triggerStep.InputValueForKeyMatchesOutputValue("sentiment", outputs, out var sentimentReason, StringComparison.OrdinalIgnoreCase))
        {
            reason = sentimentReason;
            return false;
        }
        if (!triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out var segmentReason))
        {
            reason = segmentReason;
            return false;
        }
        reason = $"{sentimentReason} {segmentReason}";
        return true;
    }
}

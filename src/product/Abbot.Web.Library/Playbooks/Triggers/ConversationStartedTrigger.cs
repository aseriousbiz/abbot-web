using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers playbooks when a conversation by a supportee is started.
/// </summary>
public class ConversationStartedTrigger : ITriggerType
{
    public const string Id = "abbot.conversation-started";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "abbot",
        Presentation = new StepPresentation
        {
            Label = "Conversation Started",
            Icon = "fa-comment",
            Description = "A conversation was started by a customer",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("segments", "Customer segments", PropertyType.CustomerSegments)
            {
                Description = "Only trigger for customers in these segments",
            },
        },
        Outputs =
        {
            new("conversation", "Conversation", PropertyType.Conversation)
            {
                Description = "The conversation that was started"
            },
            new("message", "Message", PropertyType.Message)
            {
                Description = "The message that started the conversation"
            },
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel the conversation was started in"
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer that started the conversation"
            },
        },
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
        => triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out reason);
}

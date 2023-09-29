using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers a playbook when a conversation is overdue for a reply from an agent. Can be filtered by
/// notification type: "Warning" or "Deadline".
/// </summary>
public class ConversationOverdueTrigger : ITriggerType
{
    public const string Id = "abbot.conversation-overdue";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "abbot",
        Presentation = new StepPresentation
        {
            Label = "Conversation Overdue",
            Icon = "fa-hourglass",
            Description = "A conversation is overdue for a reply",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("notification_type", "Notification Type", PropertyType.NotificationType)
            {
                Default = "warning",
            },
            new("segments", "Customer segments", PropertyType.CustomerSegments)
            {
                Description = "Only trigger for customers in these segments",
            },
        },
        Outputs =
        {
            new("conversation", "Conversation", PropertyType.Conversation)
            {
                Description = "The overdue conversation",
                ExpressionContext = "that was overdue",
            },
            new("message", "Message", PropertyType.Message)
            {
                Description = "The first message in the overdue conversation",
                ExpressionContext = "that started the overdue conversation",
            },
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel the overdue conversation is in",
                ExpressionContext = "the overdue conversation is in",
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer associated with the overdue conversation",
                ExpressionContext = "with the overdue conversation",
            },
        }
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
    {
        if (!triggerStep.InputValueForKeyMatchesOutputValue("notification_type", outputs, out var notificationTypeReason, StringComparison.OrdinalIgnoreCase))
        {
            reason = notificationTypeReason;
            return false;
        }
        if (!triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out var segmentReason))
        {
            reason = segmentReason;
            return false;
        }
        reason = $"{notificationTypeReason} {segmentReason}";
        return true;
    }
}

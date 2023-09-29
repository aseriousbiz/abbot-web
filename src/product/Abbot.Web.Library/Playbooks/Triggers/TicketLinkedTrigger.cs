using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers playbooks when a ticket is linked to a conversation.
/// </summary>
public class TicketLinkedTrigger : ITriggerType
{
    public const string Id = "abbot.ticket-linked";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "abbot",
        Presentation = new StepPresentation
        {
            Label = "Ticket Linked",
            Icon = "fa-ticket",
            Description = "Runs when a ticket is linked to a conversation",
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
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel the ticket was linked in"
            },
            new("conversation", "Conversation", PropertyType.Conversation)
            {
                Description = "The conversation the ticket was linked to"
            },
            new("message", "Message", PropertyType.Message)
            {
                Description = "The first message in the conversation the ticket was linked to"
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer the linked ticket is for"
            },
            new("ticket", "Ticket", PropertyType.Ticket)
            {
                Description = "The linked ticket"
            },
        }
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
        => triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out reason);
}

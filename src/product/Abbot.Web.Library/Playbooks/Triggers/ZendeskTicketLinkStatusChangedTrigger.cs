using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// Triggers playbooks when a Zendesk linked ticket status changes.
/// </summary>
public class ZendeskTicketLinkStatusChangedTrigger : ITriggerType
{
    public const string Id = "zendesk.ticket-link-status-changed";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "zendesk",
        Presentation = new StepPresentation
        {
            Label = "Zendesk Ticket Status Change",
            Icon = "fa-ticket",
            Description = "Runs when a linked Zendesk ticket status changes",
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
            IntegrationType.Zendesk,
        },
        Outputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Description = "The channel with the ticket status change"
            },
            new("conversation", "Conversation", PropertyType.Conversation)
            {
                Description = "The conversation with the ticket status change"
            },
            new("message", "Message", PropertyType.Message)
            {
                Description = "The first message in the conversation with the ticket status change"
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer the linked ticket with the status change is for"
            },
            new("ticket", "Ticket", PropertyType.Ticket)
            {
                Description = "The ticket with the status change"
            },
        }
    };

    public bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
        => triggerStep.CustomerSegmentsMatchTriggerFilter(outputs, out reason);
}

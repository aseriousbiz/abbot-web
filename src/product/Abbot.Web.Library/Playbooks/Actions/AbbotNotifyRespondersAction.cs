using System.ComponentModel.DataAnnotations;
using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// Sends a notification to the responders associated with a channel. The notification is either a group DM
/// or a message to the Hub for the channel if one exists.
/// </summary>
public class AbbotNotifyRespondersAction : ActionType<AbbotNotifyRespondersAction.Executor>
{
    public override StepType Type { get; } = new("abbot.notify-responders", StepKind.Action)
    {
        Category = "abbot",
        Presentation = new()
        {
            Label = "Notify Responders",
            Icon = "fa-bell",
            Description = "Notifies the first responders for a channel in its Hub or in a DM",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Description =
                    "The responders for this channel will be notified, in the channel's Hub or in a DM. " +
                    "Omit to notify default Hub/responders.",
            },
            new("headline", "Title", PropertyType.SlackMrkdwn(1))
            {
                Required = true,
                Description = "The title of the notification message",
                Placeholder = ":loudspeaker: Customer Trial Expiring Soon!"
            },
            new("mrkdwn", "Message", PropertyType.SlackMrkdwn())
            {
                Required = true,
                OldNames = new[] { "message" },
                Description = "The body of the notification message",
                Placeholder = "The customer trial for Acme Corp is expiring in 7 days."
            }
        },
    };

    public class Executor : IActionExecutor
    {
        readonly ISlackResolver _slackResolver;
        readonly IPublishEndpoint _publishEndpoint;

        public Executor(ISlackResolver slackResolver, IPublishEndpoint publishEndpoint)
        {
            _slackResolver = slackResolver;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var channelId = context.Get<string>("channel");
            var headline = context.ExpectMrkdwn("headline");
            var message = context.ExpectMrkdwn("mrkdwn", "message");

            var organization = context.Playbook.Organization;
            var room = channelId is { Length: > 0 }
                ? (await _slackResolver.ResolveRoomAsync(channelId, organization, forceRefresh: false))
                    ?? throw new ValidationException($"Channel '{channelId}' not found")
                : null;

            var escalation = false; // TODO: Dropdown?

            await _publishEndpoint.Publish(new PublishRoomNotification
            {
                OrganizationId = organization,
                RoomId = room,
                Notification = new("", headline, message, escalation),
            });

            return new(StepOutcome.Succeeded);
        }
    }
}

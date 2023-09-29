using System.ComponentModel.DataAnnotations;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Slack;

namespace Serious.Abbot.Playbooks.Actions;

public class SlackSendMessageAction : ActionType<SlackSendMessageAction.Executor>
{
    public override StepType Type { get; } = new("slack.send-message", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Send Message",
            Icon = "fa-message-plus",
            Description = "Posts a message in a channel or thread",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("message_target", "Channel or Thread", PropertyType.MessageTarget)
            {
                Description = "The channel or thread in which the message should be posted",
                Required = true,
                OldNames = new[] { "channel" },
            },
            new("mrkdwn", "Message", PropertyType.SlackMrkdwn())
            {
                Description = "The message to post",
                Placeholder = "Hello, world!",
                Required = true,
                OldNames = new[] { "message" },
            },
        },
        Outputs =
        {
            new("message_id", "Message ID", PropertyType.String)
            {
                Description = "The ID of the message that was posted",
            },
        },
    };

    public class Executor : IActionExecutor
    {
        readonly ISlackResolver _slackResolver;
        readonly ISlackApiClient _slackApiClient;

        public Executor(ISlackResolver slackResolver, ISlackApiClient slackApiClient)
        {
            _slackResolver = slackResolver;
            _slackApiClient = slackApiClient;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            if (!context.TryGetUnprotectedApiToken(out var apiToken, out var tokenMissingResult))
            {
                return tokenMissingResult;
            }

            var (channelId, threadTs) = context.ExpectMessageTarget("message_target", "channel");
            var organization = context.Playbook.Organization;

            var message = context.ExpectMrkdwn("mrkdwn", "message");

            var room = await _slackResolver.ResolveRoomAsync(channelId, organization, forceRefresh: false);
            if (room is null)
            {
                throw new ValidationException($"Channel '{channelId}' not found");
            }

            var response = await _slackApiClient.PostMessageWithRetryAsync(
                apiToken,
                new()
                {
                    Text = message,
                    Channel = channelId,
                    ThreadTs = threadTs,
                });

            if (!response.Ok)
            {
                return new(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(response, "Could not send message to Slack"),
                };
            }

            return new(StepOutcome.Succeeded)
            {
                Outputs =
                {
                    ["message_id"] = response.Body.Timestamp,
                },
                Notices = new[]
                {
                    new Notice(NoticeType.Information, $"Message sent to {room.Name} ({room.PlatformRoomId})", message)
                }
            };
        }
    }
}

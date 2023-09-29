using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Serialization;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using SlackActions = Serious.Slack.BlockKit.Actions;

namespace Serious.Abbot.Playbooks.Actions;

public class SlackPollAction : ActionType<SlackPollAction.Executor>
{
    public const string ActionsBlockId = "PollActions";
    public const string StateMessageId = "poll_message_id";
    public const string StateThreadId = "poll_thread_id";
    public const string StateMessageBlocks = "poll_message_blocks";

    public override StepType Type { get; } = new("slack.send-poll", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Send Slack Poll",
            Icon = "fa-poll-people",
            Description = "Posts a Slack poll to a channel or thread",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("message_target", "Channel or Thread", PropertyType.MessageTarget)
            {
                Description = "Channel or thread to receive the poll question",
                Required = true,
                OldNames = new[] { "channel" },
            },
            new("mrkdwn", "Poll Question", PropertyType.SlackMrkdwn(4))
            {
                Description = "Message for the poll",
                Required = true,
                OldNames = new[] { "message" },
            },
            new("options", "Poll Options", PropertyType.Poll)
            {
                Description = "Preset poll options",
                Default = "likert-5-satisfied",
                Required = true,
            },
        },
        Outputs =
        {
            new ("poll_response", "Poll Response", PropertyType.SelectedOption)
            {
                ExpressionContext = "to the sent Slack poll",
            },
        }
    };

    public class Executor : IActionExecutor, ISuspendableExecutor
    {
        readonly ISlackResolver _slackResolver;
        readonly ISlackApiClient _slackApiClient;
        readonly IClock _clock;
        readonly ILogger<Executor> _logger;

        public Executor(ISlackResolver slackResolver, ISlackApiClient slackApiClient, IClock clock, ILogger<Executor> logger)
        {
            _slackResolver = slackResolver;
            _slackApiClient = slackApiClient;
            _clock = clock;
            _logger = logger;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            if (!context.TryGetUnprotectedApiToken(out var apiToken, out var tokenMissingResult))
            {
                return tokenMissingResult;
            }

            var (channelId, threadTs) = context.ExpectMessageTarget("message_target", "channel");
            var message = context.ExpectMrkdwn("mrkdwn", "message");

            if (context.ResumeState is not null)
            {
                // NOTE: If we change this, we have to remember that we could be being resumed with state from an old version!
                var messageId = context.ResumeState[StateMessageId].Require<string>();
                var threadId = GetThreadId(context);
                var actor = context.ResumeState["actor"].Require<IDictionary<string, object?>>();
                var actorUserPlatformId = actor["id"].Require<string>();
                var resumedAt = context.ConsumeContext.SentTime ?? _clock.UtcNow;
                _logger.Resumed(resumedAt, messageId, threadId);

                await ReplaceActionsBlock(context,
                    new Section(new MrkdwnText(
                        $"_<@{actorUserPlatformId}> responded to poll {SlackFormatter.FormatTime(resumedAt)}_")));

                return new StepResult(StepOutcome.Succeeded)
                {
                    Outputs = new Dictionary<string, object?>
                    {
                        ["channel"] = channelId,
                        ["actor"] = actor,
                        ["poll_response"] = context.ResumeState["poll_response"],
                    },
                };
            }

            var options = context.Expect<OptionsDefinition>("options");

            var organization = context.Playbook.Organization;
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
                    Blocks = new ILayoutBlock[]
                    {
                        new Section(new MrkdwnText(message)),
                        new SlackActions(ActionsBlockId,
                            options.Options
                                .Select((o, i) => new ButtonElement(o.Label, o.Value)
                                {
                                    ActionId = InteractionCallbackInfo.For<PlaybookPollHandler>(
                                        PlaybookPollHandler.Context(context, i)),
                                })
                                .Cast<IActionElement>()
                                .ToArray()),
                    },
                });

            if (!response.Ok)
            {
                return new(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(response, "Could not send poll to Slack"),
                };
            }

            // TODO: Schedule a timeout?

            return new StepResult(StepOutcome.Suspended)
            {
                SuspendPresenter = "_SlackPollSuspendPresenter",
                SuspendState =
                {
                    ["channel"] = channelId,
                    [StateMessageId] = response.Body.Timestamp.Require(),
                    [StateThreadId] = response.Body.ThreadTimestamp,
                    [StateMessageBlocks] = AbbotJsonFormat.Default.Serialize(response.Body.Blocks),
                },
            };
        }

        public async Task DisposeSuspendedStepAsync(StepContext context)
        {
            if (context.ResumeState is null)
            {
                return;
            }

            var resumedAt = context.ConsumeContext.SentTime ?? _clock.UtcNow;
            _logger.CancelledResume(resumedAt);

            await ReplaceActionsBlock(context,
                new Section(new MrkdwnText(
                    $"_Poll cancelled {SlackFormatter.FormatTime(resumedAt)}_")));
        }

        async Task ReplaceActionsBlock(StepContext context, ILayoutBlock replacementBlock)
        {
            var run = context.ConsumeContext.GetPayload<PlaybookRun>();
            var organization = run.Playbook.Organization;

            if (!organization.TryGetUnprotectedApiToken(out var apiToken))
            {
                _logger.OrganizationHasNoSlackApiToken();
                return;
            }

            var (channelId, _) = context.ExpectMessageTarget("message_target", "channel");

            // NOTE: If we change this, we have to remember that we could be being resumed with state from an old version!
            if (context.ResumeState?.TryGetValue(StateMessageId, out var messageIdObj) != true
                || messageIdObj is not string messageId)
            {
                // No message to update, somehow
                return;
            }

            var threadId = GetThreadId(context);

            if (!context.ResumeState.TryGetValue(StateMessageBlocks, out var blocksJson)
                || AbbotJsonFormat.Default.Deserialize<List<ILayoutBlock>>(blocksJson.Require<string>()) is not { } blocks)
            {
                return;
            }

            blocks.ReplaceBlockById<ILayoutBlock>(ActionsBlockId, _ => replacementBlock);

            var updateResponse = await _slackApiClient.UpdateMessageAsync(apiToken,
                new()
                {
                    Timestamp = messageId,
                    ThreadTs = threadId,
                    Channel = channelId,
                    Blocks = blocks,
                });

            // Not fatal
            if (!updateResponse.Ok)
            {
                _logger.ErrorCallingSlackApi(updateResponse.ToString());
            }
        }

        static string? GetThreadId(StepContext context) =>
            context.ResumeState?.TryGetValue(StateThreadId, out var threadIdObj) == true
                && threadIdObj is string threadId
                ? threadId
                : null;
    }
}

public static partial class SlackPollActionLoggingExtensions
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Resumed at {ResumeTime} by response to poll message {PollMessageId} (thread: {PollThreadId})")]
    public static partial void Resumed(this ILogger<SlackPollAction.Executor> logger,
        DateTime resumeTime,
        string pollMessageId,
        string? pollThreadId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Cancelled poll at {CancelledTime}")]
    public static partial void CancelledResume(this ILogger<SlackPollAction.Executor> logger, DateTime cancelledTime);
}

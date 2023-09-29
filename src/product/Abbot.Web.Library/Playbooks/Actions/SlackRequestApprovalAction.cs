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

namespace Serious.Abbot.Playbooks.Actions;

public class SlackRequestApprovalAction : ActionType<SlackRequestApprovalAction.Executor>
{
    public const string ActionsBlockId = "ResponseActions";
    public const string StateMessageId = "approval_message_id";
    public const string StateMessageBlocks = "approval_message_blocks";

    const string ApprovedResponse = "approved";
    const string DeniedResponse = "denied";
    const string TimedOutResponse = "timedout";

    public override StepType Type { get; } = new("slack.request-approvers", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Request Approval in Slack",
            Icon = "fa-stamp",
            Description = "Requests an approval from one of any number of approvers in Slack",
        },
        Inputs =
        {
            new("approvers", "Approvers", PropertyType.Members)
            {
                Description = "User(s) who can approve this request",
                Required = true,
            },
            new("mrkdwn", "Message", PropertyType.SlackMrkdwn(4))
            {
                Description = "The message to be sent to approvers, who will be prompted to answer 'Yes' or 'No'",
                Required = true,
            },
        },
        Outputs =
        {
            new("approval_response", "Approval Response", PropertyType.String)
            {
                Description = "The response from the approver",
            },
            new("approval_responder", "Responder", PropertyType.Member)
            {
                Description = "The user who approved or denied the request",
            },
        },
        Branches =
        {
            new (ApprovedResponse, "If the request is approved, run these steps"),
            new (DeniedResponse, "If the request is denied, run these steps"),
        }
    };

    public class Executor : IActionExecutor, ISuspendableExecutor
    {
        readonly ISlackResolver _slackResolver;
        readonly ISlackApiClient _slackApiClient;
        readonly IClock _clock;
        readonly ILogger<Executor> _logger;

        public Executor(ISlackResolver slackResolver, ISlackApiClient slackApiClient, IClock clock,
            ILogger<Executor> logger)
        {
            _slackResolver = slackResolver;
            _slackApiClient = slackApiClient;
            _clock = clock;
            _logger = logger;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            if (context.ResumeState is not null)
            {
                return await ResumeAsync(context, context.ResumeState);
            }

            return await PostApprovalRequestAsync(context);
        }

        async Task<StepResult> PostApprovalRequestAsync(StepContext context)
        {
            if (!context.TryGetUnprotectedApiToken(out var apiToken, out var tokenMissingResult))
            {
                return tokenMissingResult;
            }

            var message = context.ExpectMrkdwn("mrkdwn");
            var members = context.Expect<string[]>("approvers");

            // Resolve a conversation for the members
            var createConvoResponse = await _slackApiClient.Conversations.OpenDirectMessageAsync(apiToken, members);
            if (!createConvoResponse.Ok)
            {
                return new(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(createConvoResponse.Require(),
                        "Could not resolve conversation for approvers"),
                };
            }

            var conversationId = createConvoResponse.Body.Id;
            var messageRequest = PlaybookSelectResponseHandler.RenderRequest(
                context,
                ActionsBlockId,
                message,
                conversationId,
                new[] { new Option("Yes", "approved"), new Option("No", "denied"), });

            var response = await _slackApiClient.PostMessageWithRetryAsync(
                apiToken,
                messageRequest);

            if (!response.Ok)
            {
                return new(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(response, "Could not send approval request to Slack"),
                };
            }

            // TODO: Schedule a timeout?

            return new StepResult(StepOutcome.Suspended)
            {
                SuspendPresenter = "_SlackRequestApprovalSuspendPresenter",
                SuspendState =
                {
                    ["channel"] = conversationId,
                    [StateMessageId] = response.Body.Timestamp.Require(),
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

            var messageId = context.ResumeState[StateMessageId].Require<string>();
            var conversationId = context.ResumeState["channel"].Require<string>();
            var resumedAt = context.ConsumeContext.SentTime ?? _clock.UtcNow;
            _logger.CancelledResume(resumedAt);

            await ReplaceActionsBlock(context,
                conversationId,
                messageId,
                new Section(new MrkdwnText(
                    $"_Request cancelled {SlackFormatter.FormatTime(resumedAt)}_")));
        }

        async Task<StepResult> ResumeAsync(StepContext context, IDictionary<string, object?> resumeState)
        {
            // NOTE: If we change this, we have to remember that we could be being resumed with state from an old version!
            var messageId = resumeState[StateMessageId].Require<string>();
            var conversationId = resumeState["channel"].Require<string>();
            var actor = resumeState["actor"].Require<IDictionary<string, object?>>();
            var actorUserPlatformId = actor["id"].Require<string>();
            var actorName = actor["name"].Require<string>();
            var resumedAt = context.ConsumeContext.SentTime ?? _clock.UtcNow;
            _logger.Resumed(resumedAt, messageId);

            var response = AbbotJsonFormat.Default.Convert<SelectedOption>(resumeState["selection_response"]).Require();
            await ReplaceActionsBlock(context,
                conversationId,
                messageId,
                new Section(new MrkdwnText(
                    $"_<@{actorUserPlatformId}> {response.Value} this request {SlackFormatter.FormatTime(resumedAt)}_")));

            var (branch, description) = response.Value switch
            {
                ApprovedResponse => (ApprovedResponse, $"{actorName} approved the request"),
                TimedOutResponse => (DeniedResponse, "The approval request timed out"),
                _ => (DeniedResponse, $"{actorName} denied the request")
            };

            var sequence = context.Step.Branches.TryGetValue(branch, out var s)
                ? s
                : null;

            return new StepResult(StepOutcome.Succeeded)
            {
                Outputs = new Dictionary<string, object?>
                {
                    ["approval_responder"] = actor,
                    ["approval_response"] = response.Value,
                },
                CallBranch = new(branch, sequence, description),
            };
        }

        async Task<(string? Address, ApiResponse? Response)> GetGroupDmAddressAsync(string apiToken, IReadOnlyList<string> mentionIds)
        {
            var openGroupDmRequest = OpenConversationRequest.FromUsers(mentionIds);
            var response = await _slackApiClient.Conversations.OpenConversationAsync(
                apiToken,
                openGroupDmRequest);

            if (response.Ok)
            {
                return (response.Body.Id, response);
            }

            return (null, response);
        }

        async Task ReplaceActionsBlock(StepContext context, string conversationId, string messageId, ILayoutBlock replacementBlock)
        {
            var run = context.ConsumeContext.GetPayload<PlaybookRun>();
            var organization = run.Playbook.Organization;

            if (!organization.TryGetUnprotectedApiToken(out var apiToken))
            {
                _logger.OrganizationHasNoSlackApiToken();
                return;
            }

            if (!context.ResumeState.Require().TryGetValue(StateMessageBlocks, out var blocksJson)
                || AbbotJsonFormat.Default.Deserialize<List<ILayoutBlock>>(blocksJson.Require<string>()) is not
                { } blocks)
            {
                return;
            }

            blocks.ReplaceBlockById<ILayoutBlock>(ActionsBlockId, _ => replacementBlock);

            var updateResponse = await _slackApiClient.UpdateMessageAsync(apiToken,
                new()
                {
                    Timestamp = messageId,
                    Channel = conversationId,
                    Blocks = blocks,
                });

            // Not fatal
            if (!updateResponse.Ok)
            {
                _logger.ErrorCallingSlackApi(updateResponse.ToString());
            }
        }
    }
}

public static partial class SlackRequestApprovalActionLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Resumed at {ResumeTime} by response to message {MessageId}")]
    public static partial void Resumed(this ILogger<SlackRequestApprovalAction.Executor> logger, DateTime resumeTime,
        string messageId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Cancelled at {CancelledTime}")]
    public static partial void CancelledResume(this ILogger<SlackRequestApprovalAction.Executor> logger,
        DateTime cancelledTime);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed to create group DM for {UserList}: {SlackError}")]
    public static partial void ErrorCreatingGroupDM(
        this ILogger<SlackRequestApprovalAction.Executor> logger,
        string? userList,
        string slackError);
}

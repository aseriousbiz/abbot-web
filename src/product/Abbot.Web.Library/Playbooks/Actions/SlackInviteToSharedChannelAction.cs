using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MassTransit;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Serialization;
using Serious.Slack;

namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// A Playbook action that invites a user to a Slack Connect channel.
/// </summary>
public class SlackInviteToSharedChannelAction : ActionType<SlackInviteToSharedChannelAction.Executor>
{
    const string AcceptedResponse = "accepted";
    const string DeclinedResponse = "declined";

    public override StepType Type { get; } = new("slack.invite-to-shared-channel", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Invite to Slack Connect",
            Icon = "fa-phone-office",
            Description =
                "Creates a Slack Connect channel and invites a user to it and then waits for a user to respond to the invitation or for the invitation to time out.",
        },
        Inputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Description =
                    "The channel to invite the external user to. This will make the channel a Slack Connect channel.",
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Description =
                    "This must be a customer with an email address, supplied by the Customer Info Submitted trigger.",
                Hidden = true,
                Default = "{{ trigger.outputs.customer }}",
            },
            new("expiration", "Expiration", PropertyType.String)
            {
                Hidden = true,
                Default =
                    "15.00:00:00", // Slack invitations expire in 14 days. We'll expire the playbook run in 15 days to be safe.
                Description =
                    "The amount of time to wait for the invitation to be accepted. If not specified, the default is 15 days.",
            },
        },
        Outputs =
        {
            new("invitation_response", "Invitation Response", PropertyType.String)
            {
                ExpressionContext = "to the Slack Connect invitation",
            },
        },
        Branches =
        {
            new(AcceptedResponse, "If the invitation is accepted, run these steps"),
            new(DeclinedResponse, "If the invitation is declined or times out, run these steps"),
        },
        RequiredTriggers =
        {
            CustomerInfoSubmittedTrigger.Id
        }
    };

    public class Executor : IActionExecutor, ISuspendableExecutor
    {
        readonly IConversationsApiClient _conversationsApiClient;
        readonly SharedChannelInviteHandler _sharedChannelInviteHandler;
        readonly IRoomRepository _roomRepository;
        readonly IUserRepository _userRepository;
        readonly CustomerRepository _customerRepository;
        readonly IClock _clock;

        public Executor(
            IConversationsApiClient conversationsApiClient,
            SharedChannelInviteHandler sharedChannelInviteHandler,
            IRoomRepository roomRepository,
            IUserRepository userRepository,
            CustomerRepository customerRepository,
            IClock clock)
        {
            _conversationsApiClient = conversationsApiClient;
            _sharedChannelInviteHandler = sharedChannelInviteHandler;
            _roomRepository = roomRepository;
            _userRepository = userRepository;
            _customerRepository = customerRepository;
            _clock = clock;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            if (context.ResumeState is not null)
            {
                return Resume(context, context.ResumeState);
            }

            return await SendSlackConnectInviteAsync(context);
        }

        async Task<StepResult> SendSlackConnectInviteAsync(StepContext context)
        {
            if (!context.TryGetUnprotectedApiToken(out var apiToken, out var tokenMissingResult))
            {
                return tokenMissingResult;
            }

            var channelId = context.Expect<string>("channel");
            var organization = context.Playbook.Organization;
            var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(channelId, organization);
            if (room is null)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.NotFound($"Abbot needs to be added to {channelId}"),
                };
            }

            // Check if we're dealing with a customer object or a customer ID
            if (!context.Inputs.TryGetValue("customer", out var customerInput))
            {
                throw new ValidationException($"Input 'customer' is required.");
            }

            string? invitee = null;
            if (customerInput is string customerInputStr)
            {
                if (!int.TryParse(customerInputStr, out var customerId))
                {
                    throw new ValidationException("Input 'customer' must be a valid integer.");
                }

                var customer = await _customerRepository.GetByIdAsync(customerId, organization);
                if (customer is null)
                {
                    return new StepResult(StepOutcome.Failed)
                    {
                        Problem = Problems.NotFound($"Customer {customerId} not found."),
                    };
                }

                invitee = customer.Properties.PrimaryContactEmail;
            }
            else if (customerInput is not null
                     && StepContext.JsonFormat.Convert<SubmittedCustomerInfo>(customerInput) is { } customer)
            {
                invitee = customer.Email;
            }
            else
            {
                throw new ValidationException("Unknown customer input type.");
            }

            if (invitee is not { Length: > 0 })
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.ArgumentError(
                        "customer",
                        "There is no primary contact for the customer."),
                };
            }

            var response = invitee.Contains('@', StringComparison.Ordinal)
                ? await _conversationsApiClient.InviteToSlackConnectChannelViaEmailAsync(
                    apiToken,
                    channelId,
                    invitee,
                    externalLimited: false)
                : await _conversationsApiClient.InviteToSlackConnectChannelViaUserIdAsync(
                    apiToken,
                    channelId,
                    invitee,
                    externalLimited: false);

            if (!response.Ok)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(response,
                        $"Invitation to connect channel {room.Name} failed"),
                };
            }

            var inviteId = response.InviteId;
            var abbot = await _userRepository.EnsureAbbotUserAsync();
            await _sharedChannelInviteHandler.StoreInvitationContextAsync(context, inviteId, abbot, organization);

            var expirationOffset = context.ExpectParseable<TimeSpan>("expiration");
            var expirationTime = _clock.UtcNow.Add(expirationOffset);
            var scheduled = await context.ConsumeContext.SchedulePublish(
                expirationTime,
                new ResumeSuspendedStep(context.PlaybookRun.CorrelationId, context.ActionReference));

            return new StepResult(StepOutcome.Suspended)
            {
                SuspendPresenter = "_SlackInviteToSharedChannelPresenter",
                SuspendedUntil = expirationTime,
                SuspendState =
                {
                    ["expiration"] = expirationTime.ToString("O"),
                    ["resume_publish_id"] = scheduled.TokenId,
                },
                Notices = new List<Notice>
                {
                    new(NoticeType.Information,
                        "Invitation sent",
                        $"Invitation to connect channel {room.Name} with Id "
                        + $"{inviteId} sent to {invitee}.\nPlaybook will continue when the invitation is accepted or "
                        + $"expires {expirationTime:R}."),
                }
            };
        }

        static StepResult Resume(StepContext context, IDictionary<string, object?> resumeState)
        {
            var invitationResponse = resumeState.TryGetValue("invitation_response", out var resp)
                ? resp
                : DeclinedResponse;

            var (branch, description, notice) = invitationResponse switch
            {
                AcceptedResponse => (AcceptedResponse, $"The invitation was accepted",
                    new Notice(NoticeType.Information, "Invitation accepted.")),
                DeclinedResponse => (DeclinedResponse, "The invitation was declined, or timed out",
                    new Notice(NoticeType.Warning, "Invitation declined or timed out.")),
                _ => (DeclinedResponse, "Unexpected response",
                    new Notice(NoticeType.Warning, $"Unexpected response: {invitationResponse}")),
            };

            var sequence = context.Step.Branches.TryGetValue(branch, out var s)
                ? s
                : null;

            return new StepResult(StepOutcome.Succeeded)
            {
                Outputs = new Dictionary<string, object?>
                {
                    ["invitation_response"] = invitationResponse,
                },
                Notices = new List<Notice>
                {
                    notice,
                },
                CallBranch = new(branch, sequence, description),
            };
        }

        public async Task DisposeSuspendedStepAsync(StepContext context)
        {
            if (context.ResumeState is not { } suspendState)
            {
                return;
            }

            // NOTE: If we change this, we have to remember that we could be being resumed with state from an old version!
            var token = Guid.Parse(suspendState["resume_publish_id"].Require().ToString()!);
            var scheduler = context.ConsumeContext.GetPayload<IMessageScheduler>();
            await scheduler.CancelScheduledPublish<ResumeSuspendedStep>(token);
        }
    }
}

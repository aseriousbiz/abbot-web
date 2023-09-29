using Humanizer;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// Creates a channel for a customer by using the customer name and a chosen prefix.
/// </summary>
public class SlackCreateChannelForCustomerAction : ActionType<SlackCreateChannelForCustomerAction.Executor>
{
    public override StepType Type { get; } = new("slack.create-channel-for-customer", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Create Channel for Customer",
            Icon = "fa-phone-office",
            Description = "Creates a channel for a customer using the customer name and a chosen prefix",
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("prefix", "Channel Prefix", PropertyType.String)
            {
                Description = "The channel will be created with the name of the customer prefixed with this value.",
            },
            new("customer", "Customer", PropertyType.Customer)
            {
                Required = true,
            },
            new("private", "Make channel private", PropertyType.Boolean) { Default = false },
        },
        Outputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                ExpressionContext = "that was created for the customer",
            },
        }
    };

    public class Executor : IActionExecutor
    {
        readonly CustomerRepository _customerRepository;
        readonly ISlackResolver _slackResolver;
        readonly IUserRepository _userRepository;
        readonly IConversationsApiClient _apiClient;

        public Executor(
            CustomerRepository customerRepository,
            ISlackResolver slackResolver,
            IUserRepository userRepository,
            IConversationsApiClient apiClient)
        {
            _customerRepository = customerRepository;
            _slackResolver = slackResolver;
            _userRepository = userRepository;
            _apiClient = apiClient;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            if (!context.TryGetUnprotectedApiToken(out var apiToken, out var tokenMissingResult))
            {
                return tokenMissingResult;
            }

            var prefix = context.Get<string>("prefix");
            var customerIdText = context.Expect<string>("customer");
            var isPrivate = context.Get<bool>("private");

            if (!int.TryParse(customerIdText, out var customerId))
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.NotFound("Customer id not in the outputs", $"Customer `{customerIdText}` not found")
                };
            }

            var organization = context.Playbook.Organization;
            var customer = await _customerRepository.GetByIdAsync(customerId, organization);

            if (customer is null)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.NotFound("Customer not found", $"Customer with Id `{customerIdText}` not found")
                };
            }

            string channelName = CreateChannelName(prefix, customer.Name);

            var request = new ConversationCreateRequest(channelName, isPrivate);

            var response = await _apiClient.CreateConversationAsync(apiToken, request);
            if (!response.Ok)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(
                        response,
                        "Could not create channel",
                        $"Channel name: {channelName}")
                };
            }

            // We don't need to invite Abbot to the channel because Abbot created the channel, which makes it
            // a member of the channel automatically.
            var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);
            var room = await _slackResolver.UpdateFromConversationInfoAsync(null, response.Body, organization);
            await _customerRepository.AssignRoomAsync(room, customer, abbot);

            var outputs = new OutputsBuilder()
                .SetRoom(room)
                .Outputs;

            return new StepResult(StepOutcome.Succeeded)
            {
                Outputs = outputs,
                Notices = new[]
                {
                    new Notice(NoticeType.Information, "Channel name", room.Name),
                }
            };
        }

        static string CreateChannelName(string? prefix, string customerName)
        {
            if (prefix is { Length: > 0 })
            {
                prefix = prefix.ToLowerInvariant().Kebaberize();
                if (!prefix.EndsWith('-'))
                {
                    prefix += "-";
                }
            }

            var channelName = prefix + customerName.ToSlackChannelName();
            return channelName;
        }
    }
}


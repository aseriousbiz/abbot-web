using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Playbooks.Actions;

/// <summary>
/// Adds the Abbot bot to a channel.
/// </summary>
public class SlackAddAbbotToChannelAction : ActionType<SlackAddAbbotToChannelAction.Executor>
{
    public override StepType Type { get; } = new("slack.add-abbot-to-channel", StepKind.Action)
    {
        Category = "slack",
        Presentation = new()
        {
            Label = "Add Abbot to Channel",
            Icon = "fa-robot",
            Description = "Invites Abbot to a channel"
        },
        RequiredIntegrations =
        {
            IntegrationType.SlackApp
        },
        Inputs =
        {
            new("channel", "Channel", PropertyType.Channel)
            {
                Required = true,
            },
        },
        RequiredFeatureFlags =
        {
            FeatureFlags.PlaybookStepsWave1,
        }
    };

    public class Executor : IActionExecutor
    {
        readonly IConversationsApiClient _apiClient;
        readonly IUserRepository _userRepository;

        public Executor(IConversationsApiClient apiClient, IUserRepository userRepository)
        {
            _apiClient = apiClient;
            _userRepository = userRepository;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var organization = context.Playbook.Organization;
            var channel = context.Require<string>("channel");

            if (!context.TryGetUnprotectedApiToken(out var apiToken, out var tokenMissingResult))
            {
                return tokenMissingResult;
            }

            var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

            var inviteRequest = new UsersInviteRequest(channel, new[] { abbot.User.PlatformUserId });
            var response = await _apiClient.InviteUsersToConversationAsync(apiToken, inviteRequest);
            if (!response.Ok)
            {
                return new StepResult(StepOutcome.Failed)
                {
                    Problem = Problems.FromSlackErrorResponse(response, "Could not invite Abbot to channel")
                };
            }

            return new StepResult(StepOutcome.Succeeded);
        }
    }
}

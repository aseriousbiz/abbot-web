using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;

namespace Serious.Abbot.Playbooks.Actions;

public class SystemSkillAction : ActionType<SystemSkillAction.Executor>
{
    public override StepType Type { get; } = new("system.skill", StepKind.Action)
    {
        Category = "system",
        Presentation = new()
        {
            Label = "Run Skill",
            Icon = "fa-circle-play",
            Description = "Runs a Skill in a channel",
        },
        Inputs =
        {
            new("skill", "Run Skill", PropertyType.Skill)
            {
                Description = "The skill to execute",
                Required = true,
            },
            new("arguments", "With Arguments", PropertyType.String)
            {
                Description = "The arguments to pass to the skill",
            },
            new("channel", "In Channel", PropertyType.Channel)
            {
                Description = "The channel in which to execute the skill",
                Required = true,
            },
        },
        Outputs =
        {
            new("response_content", "Raw Skill response", PropertyType.String)
            {
                Description = "The raw response from the Skill",
            },
            new("response_content_type", "Content type of Skill response", PropertyType.String)
            {
                Description = "The Content-Type of the Skill response",
            },
            new("replies", "Reply messages created by calling the skill", PropertyType.String) // TODO: Message[]?
            {
                Description = "The replies the skill posted to the channel in which it was called",
            },
        },
    };

    public class Executor : IActionExecutor
    {
        static readonly ILogger<Executor> Log = ApplicationLoggerFactory.CreateLogger<Executor>();

        readonly ISkillRepository _skillRepository;
        readonly ISkillRunnerClient _skillRunnerClient;
        readonly ISlackResolver _slackResolver;
        readonly IUserRepository _userRepository;
        readonly IUrlGenerator _urlGenerator;

        public Executor(
            ISkillRepository skillRepository,
            ISkillRunnerClient skillRunnerClient,
            ISlackResolver slackResolver,
            IUserRepository userRepository,
            IUrlGenerator urlGenerator)
        {
            _skillRepository = skillRepository;
            _skillRunnerClient = skillRunnerClient;
            _slackResolver = slackResolver;
            _userRepository = userRepository;
            _urlGenerator = urlGenerator;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            var skillName = context.Expect<string>("skill");
            var channelId = context.Expect<string>("channel");

            var organization = context.Playbook.Organization;
            var room = await _slackResolver.ResolveRoomAsync(channelId, organization, forceRefresh: false);
            if (room is null)
            {
                throw new ValidationException($"Channel '{channelId}' not found");
            }

            var skill = await _skillRepository.GetAsync(skillName, organization);
            if (skill is null)
            {
                throw new ValidationException($"Skill '{skillName}' not found.");
            }

            using var skillScope = Log.BeginSkillScope(skill);

            if (!skill.Enabled)
            {
                throw new ValidationException($"Skill '{skillName}' not enabled.");
            }

            if (skill.IsDeleted)
            {
                throw new ValidationException($"Skill '{skillName}' not found.");
            }

            // Querying the Member here to make sure the User has the Member
            var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

            var auditId = Guid.NewGuid();
            var response = await _skillRunnerClient.SendPlaybookActionTriggerAsync(
                new()
                {
                    Name = room.Name.Require(),
                    RoomId = room.PlatformRoomId,
                    Skill = skill,
                    PlaybookRun = context.PlaybookRun,
                    TriggerRequest = context.PlaybookRun.Properties.TriggerRequest,
                    SignalMessage = context.PlaybookRun.Properties.SignalMessage,
                    Arguments = context.Get<string>("arguments"),
                    Creator = abbot.User,
                },
                _urlGenerator.SkillPage(skillName),
                auditId);

            return new(response.Success ? StepOutcome.Succeeded : StepOutcome.Failed)
            {
                Problem = Problems.FromSkillRunResponse(response),
                Outputs = new Dictionary<string, object?>(response.Outputs),
            };
        }
    }
}

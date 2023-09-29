using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Skills;

public abstract class TriggerSkillBase<TTrigger> where TTrigger : SkillTrigger
{
    static readonly ILogger<TriggerSkillBase<TTrigger>> Log = ApplicationLoggerFactory.CreateLogger<TriggerSkillBase<TTrigger>>();

    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    protected TriggerSkillBase(
        ISkillRepository skillRepository,
        IPermissionRepository permissions,
        IUrlGenerator urlGenerator)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
        Url = urlGenerator;
    }

    IUrlGenerator Url { get; }

    public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (skill, description) = messageContext.Arguments;

        var task = (skill.Value, description.Value) switch
        {
            ("", _) => ReplyWithUsage(messageContext, cancellationToken),
            var (skillName, descriptionText) => SetUpTriggerAsync(
                skillName,
                descriptionText,
                messageContext),
        };

        return task;
    }

    protected abstract Task<TTrigger> CreateTriggerAsync(
        Skill skill,
        string description,
        MessageContext messageContext);

    protected abstract Task ReplyWithUsage(MessageContext messageContext, CancellationToken cancellationToken);

    async Task SetUpTriggerAsync(
        string skillName,
        string description,
        MessageContext messageContext)
    {
        if (messageContext.FromMember.Active && !messageContext.FromMember.MemberRoles.Any(ur =>
                ur.Role.Name
                    is Roles.Agent
                    or Roles.Administrator
                    or Roles.Staff))
        {
            await messageContext.SendActivityAsync(
                $"Only users who are members of the organization may {TriggerVerb} a skill. " +
                $"Visit {Url.HomePage()} to log in and request access.");
            return;
        }

        var skill = await _skillRepository.GetAsync(skillName, messageContext.Organization);
        if (skill is null)
        {
            await messageContext.SendActivityAsync(
                $"I cannot {TriggerVerb} the nonexistent skill `{skillName}`.");
            return;
        }

        if (!await _permissions.CanRunAsync(messageContext.FromMember, skill))
        {
            await messageContext.SendActivityAsync("`Use` permission for the skill is required to create a trigger.");
            return;
        }

        if (messageContext is not { Room: { Persistent: true, Name.Length: > 0 } })
        {
            await messageContext.SendActivityAsync(
                $"{VerbNounExpression} a skill can only be done in a channel, not in a Direct Message (DM), the Bot Console, etc.");
            return;
        }

        var platformRoomId = messageContext.Room.PlatformRoomId;

        var existingTrigger = skill
            .Triggers
            .OfType<TTrigger>()
            .FirstOrDefault(t => platformRoomId == t.RoomId);

        if (existingTrigger is null)
        {
            Log.TriggerDoesNotHave(typeof(TTrigger), platformRoomId, skill.Id, skill.Name);
        }
        else
        {
            Log.TriggerAlreadyExists(typeof(TTrigger), platformRoomId, skill.Id, skill.Name);
        }

        var skillTriggerPage = Url.TriggerPage(skillName);

        if (existingTrigger is not null)
        {
            await messageContext.SendActivityAsync(
                $"{GetTriggerExistsMessage(existingTrigger, skillName, messageContext)} " +
                $"Visit {skillTriggerPage} to see the list of triggers for this skill.");
            return;
        }

        await CreateTriggerAsync(skill, description, messageContext);

        await messageContext.SendActivityAsync(GetSuccessMessage(skillName, messageContext, skillTriggerPage));
    }

    protected virtual string GetTriggerExistsMessage(TTrigger trigger, string skillName, MessageContext messageContext)
    {
        return $"{TriggerNoun.Capitalize()} for `{skillName}` to the channel {messageContext.FormatRoomMention()} already exists.";
    }

    protected abstract string GetSuccessMessage(string skill, MessageContext messageContext, Uri triggerPage);

    protected abstract string TriggerVerb { get; }

    protected abstract string VerbNounExpression { get; }

    protected abstract string TriggerNoun { get; }
}

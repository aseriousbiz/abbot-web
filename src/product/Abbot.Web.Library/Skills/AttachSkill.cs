using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Skills;

[Skill(Description = "Attach a user skill to a channel so that the skill can be called via HTTP and reply to the channel without user interaction.")]
public sealed class AttachSkill : TriggerSkillBase<SkillHttpTrigger>, ISkill
{
    readonly ITriggerRepository _skillTriggerRepository;

    public AttachSkill(
        ISkillRepository skillRepository,
        ITriggerRepository skillTriggerRepository,
        IPermissionRepository permissions,
        IUrlGenerator urlGenerator)
        : base(skillRepository, permissions, urlGenerator)
    {
        _skillTriggerRepository = skillTriggerRepository;
    }

    protected override Task<SkillHttpTrigger> CreateTriggerAsync(
        Skill skill,
        string description,
        MessageContext messageContext)
    {
        return _skillTriggerRepository.CreateHttpTriggerAsync(
            skill,
            description,
            messageContext.Room.ToPlatformRoom(),
            messageContext.FromMember);
    }

    protected override Task ReplyWithUsage(MessageContext messageContext, CancellationToken cancellationToken)
    {
        return messageContext.SendHelpTextAsync(this);
    }

    protected override string GetSuccessMessage(string skill, MessageContext messageContext, Uri triggerPage)
    {
        return $"The skill `{skill}` is now attached to the channel {messageContext.FormatRoomMention()}. "
               + $"Visit {triggerPage} to get the secret URL used to call this skill.";
    }

    protected override string TriggerVerb => "attach an HTTP trigger to";
    protected override string VerbNounExpression => "Attaching an HTTP trigger to";
    protected override string TriggerNoun => "an HTTP trigger";

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{skill}", "enables the skill to receive HTTP requests and respond to this channel.");
        usage.Add("{skill} {description}", "use the description to note what will use the trigger.");
    }
}

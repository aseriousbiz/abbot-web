using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Skills;

[Skill(Description = "Set up the skill to be called on a recurring schedule and respond to this channel.")]
public sealed class ScheduleSkill : TriggerSkillBase<SkillScheduledTrigger>, ISkill
{
    readonly ITriggerRepository _skillTriggerRepository;

    public ScheduleSkill(
        ISkillRepository skillRepository,
        ITriggerRepository skillTriggerRepository,
        IPermissionRepository permissions,
        IUrlGenerator urlGenerator)
        : base(skillRepository, permissions, urlGenerator)
    {
        _skillTriggerRepository = skillTriggerRepository;
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{skill}", "enables the skill to receive to be called on a schedule and respond to this channel.");
        usage.Add("{skill} {description}", "use the description to note what will use the trigger.");
    }

    protected override Task<SkillScheduledTrigger> CreateTriggerAsync(
        Skill skill,
        string description,
        MessageContext messageContext)
    {
        return _skillTriggerRepository.CreateScheduledTriggerAsync(
            skill,
            null,
            description,
            Cron.Never(),
            messageContext.Room.ToPlatformRoom(),
            messageContext.FromMember);
    }

    protected override Task ReplyWithUsage(MessageContext messageContext, CancellationToken cancellationToken)
    {
        return messageContext.SendHelpTextAsync(this);
    }

    protected override string GetSuccessMessage(string skill, MessageContext messageContext, Uri triggerPage)
    {
        return $"The skill `{skill}` is now scheduled to respond to the channel {messageContext.FormatRoomMention()}. "
               + $"Visit {triggerPage} to configure the schedule.";
    }

    protected override string GetTriggerExistsMessage(SkillScheduledTrigger trigger, string skillName, MessageContext messageContext)
    {
        return $"The skill `{skillName}` is already scheduled to run {trigger.CronScheduleDescription.UnCapitalize()}.";
    }

    protected override string TriggerVerb { get; } = "schedule";
    protected override string VerbNounExpression { get; } = "Scheduling";
    protected override string TriggerNoun { get; } = "A scheduled trigger";

}

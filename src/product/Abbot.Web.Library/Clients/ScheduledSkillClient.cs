using System.ComponentModel;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;
using TimeZoneConverter;

namespace Serious.Abbot.Clients;

/// <summary>
/// Client used to schedule and run skills on a schedule. The <see cref="SkillScheduledTrigger"/>
/// contains information about the schedule and skill to run.
/// </summary>
public class ScheduledSkillClient : IScheduledSkillClient
{
    static readonly ILogger<ScheduledSkillClient> Log = ApplicationLoggerFactory.CreateLogger<ScheduledSkillClient>();

    readonly ITriggerRepository _triggerRepository;
    readonly ISkillRunnerClient _skillRunnerClient;
    readonly IRecurringJobManager _recurringJobManager;
    readonly IUrlGenerator _urlGenerator;

    public ScheduledSkillClient(
        ITriggerRepository triggerRepository,
        ISkillRunnerClient skillRunnerClient,
        IRecurringJobManager recurringJobManager,
        IUrlGenerator urlGenerator)
    {
        _triggerRepository = triggerRepository;
        _skillRunnerClient = skillRunnerClient;
        _recurringJobManager = recurringJobManager;
        _urlGenerator = urlGenerator;
    }

    /// <summary>
    /// Runs the scheduled skill. This method should be called by the `Recurring Job Manager`.
    /// </summary>
    /// <remarks>
    /// This is here so the existing scheduled jobs continue to work.
    /// </remarks>
    /// <param name="scheduledTriggerId">The id of the scheduled trigger.</param>
    [Queue(HangfireQueueNames.NormalPriority)]
    public Task RunScheduledSkillAsync(int scheduledTriggerId)
    {
        return RunScheduledSkillAsync(
            scheduledTriggerId,
            "unknown",
            0,
            "unknown",
            0);
    }

    [DisplayName("Run Scheduled Skill `{1}` (Id: {2}) for org `{3}` (Id: {4}) (Trigger Id: {0})")]
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task RunScheduledSkillAsync(
        int scheduledTriggerId,
        string skill,
        int skillId,
        string organizationName,
        int organizationId)
    {
        Log.RunScheduledSkill(skillId, skill, scheduledTriggerId, organizationId);
        var trigger = await _triggerRepository.GetScheduledTriggerAsync(scheduledTriggerId);
        if (trigger is null)
        {
            _recurringJobManager.RemoveIfExists(GetJobId(scheduledTriggerId));
            return;
        }

        if (trigger.Skill.Enabled is false)
        {
            return;
        }

        var skillUrl = _urlGenerator.SkillPage(skill);
        using var activity = ActivityHelper.CreateAndStart<ScheduledSkillClient>();
        await _skillRunnerClient.SendScheduledTriggerAsync(trigger, skillUrl, Guid.NewGuid());
    }

    /// <summary>
    /// Schedules a skill to be run according to its trigger.
    /// </summary>
    /// <param name="scheduledTrigger">The scheduled trigger for the skill.</param>
    /// <returns>The job id for the schedule</returns>
    public string ScheduleSkill(SkillScheduledTrigger scheduledTrigger)
    {
        var jobId = GetJobId(scheduledTrigger.Id);
        Log.AttemptToCreateRecurringJob(jobId);
        _recurringJobManager.AddOrUpdate(
            jobId,
            Job.FromExpression(() => RunScheduledSkillAsync(
                scheduledTrigger.Id,
                scheduledTrigger.Skill.Name,
                scheduledTrigger.SkillId,
                scheduledTrigger.Skill.Organization.Name ?? scheduledTrigger.Skill.Organization.PlatformId,
                scheduledTrigger.Skill.OrganizationId)),
            scheduledTrigger.CronSchedule,
            TZConvert.GetTimeZoneInfo(scheduledTrigger.TimeZoneId ?? "Etc/UTC"));
        return jobId;
    }

    public void UnscheduleSkill(SkillScheduledTrigger scheduledTrigger)
    {
        var jobId = GetJobId(scheduledTrigger.Id);
        _recurringJobManager.RemoveIfExists(jobId);
    }

    static string GetJobId(int scheduledTriggerId)
    {
        return $"ScheduledSkill_{scheduledTriggerId}";
    }
}

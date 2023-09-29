using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Clients;

/// <summary>
/// Client used to schedule and run skills on a schedule. The <see cref="SkillScheduledTrigger"/>
/// contains information about the schedule and skill to run.
/// </summary>
public interface IScheduledSkillClient
{
    /// <summary>
    /// Runs the scheduled skill. This method should be called by the `Recurring Job Manager`.
    /// </summary>
    /// <remarks>
    /// This is here so the existing scheduled jobs continue to work.
    /// </remarks>
    /// <param name="scheduledTriggerId">The id of the scheduled trigger.</param>
    Task RunScheduledSkillAsync(int scheduledTriggerId);

    /// <summary>
    /// Runs the scheduled skill. This method should be called by the `Recurring Job Manager`.
    /// </summary>
    /// <remarks>
    /// The extra parameters are used to populate the [DisplayName] attribute so we get a nice
    /// readable string in the hangfire dashboard.
    /// </remarks>
    /// <param name="scheduledTriggerId">The id of the scheduled trigger.</param>
    /// <param name="skill">The name of the skill. This is only passed for the DisplayAttribute.</param>
    /// <param name="skillId">The Id of the skill. This is only passed for the DisplayAttribute.</param>
    /// <param name="organizationName">The name of the organization. This is only passed for the DisplayAttribute.</param>
    /// <param name="organizationId">The Id of the organization. This is only passed for the DisplayAttribute.</param>
    Task RunScheduledSkillAsync(
        int scheduledTriggerId,
        string skill,
        int skillId,
        string organizationName,
        int organizationId);

    /// <summary>
    /// Schedules a skill to be run according to its trigger.
    /// </summary>
    /// <param name="scheduledTrigger">The scheduled trigger for the skill.</param>
    /// <returns>The job id for the schedule</returns>
    string ScheduleSkill(SkillScheduledTrigger scheduledTrigger);

    /// <summary>
    /// Unschedules a skill from running.
    /// </summary>
    /// <param name="scheduledTrigger"></param>
    void UnscheduleSkill(SkillScheduledTrigger scheduledTrigger);
}

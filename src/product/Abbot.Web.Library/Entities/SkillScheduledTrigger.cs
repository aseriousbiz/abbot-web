using System.ComponentModel.DataAnnotations.Schema;
using CronExpressionDescriptor;
using Hangfire;
using Serious.Abbot.Messages;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A trigger that was initiated by a <see cref="Playbook"/>.
/// </summary>
[NotMapped] // Only used to communicate from Playbook to Skill
public class SkillPlaybookActionTrigger : SkillTrigger<SkillPlaybookActionTrigger>
{
    /// <summary>
    /// The args to pass to the skill. Should not include the skill name.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// The <see cref="Entities.PlaybookRun"/> that triggered the skill.
    /// </summary>
    public required PlaybookRun PlaybookRun { get; init; }

    /// <summary>
    /// The initiating <see cref="HttpTriggerRequest"/>, if applicable.
    /// </summary>
    public HttpTriggerRequest? TriggerRequest { get; set; }

    /// <summary>
    /// The initiating <see cref="SignalMessage"/>, if applicable.
    /// </summary>
    public SignalMessage? SignalMessage { get; set; }

    public override AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation) =>
        throw new NotSupportedException();
}

/// <summary>
/// A trigger that can be used to call the skill by on a scheduled basis.
/// </summary>
public class SkillScheduledTrigger : SkillTrigger<SkillScheduledTrigger>
{
    /// <summary>
    /// The schedule as a cron string.
    /// </summary>
    public string CronSchedule { get; set; } = null!;

    /// <summary>
    /// The TimeZoneId for the schedule, if needed.
    /// </summary>
    public string? TimeZoneId { get; set; } = "Etc/UTC";

    /// <summary>
    /// The args to pass to the skill. Should not include the skill name.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Returns a human friendly description of the cron schedule.
    /// </summary>
    public string CronScheduleDescription =>
        CronSchedule == Cron.Never()
            ? "Never"
            : ExpressionDescriptor.GetDescription(
                CronSchedule,
                new Options { Verbose = true, ThrowExceptionOnParseError = false });

    public override AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var auditEvent = CreateAuditEventInstance<ScheduledTriggerChangeEvent>();

        auditEvent.Description = $"{auditOperation} scheduled trigger `{Name}` with schedule `{CronScheduleDescription}` for skill `{Skill.Name}`.";
        auditEvent.CronSchedule = CronSchedule;
        auditEvent.TimeZoneId = TimeZoneId;

        return auditEvent;
    }
}

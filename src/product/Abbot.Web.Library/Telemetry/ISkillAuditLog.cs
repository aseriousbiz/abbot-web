using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Telemetry;

/// <summary>
/// Skill specific audit log.
/// </summary>
public interface ISkillAuditLog
{
    /// <summary>
    /// Logs that a skill was enabled or disabled.
    /// </summary>
    /// <param name="skill">The skill changed.</param>
    /// <param name="actor">The person enabling or disabling the skill.</param>
    Task<SkillInfoChangedAuditEvent> LogSkillEnabledChangedAsync(Skill skill, User actor);

    /// <summary>
    /// Logs non-code changes to a skill.
    /// </summary>
    /// <param name="skill">The modified skill.</param>
    /// <param name="priorVersion">The previous version of the skill prior to these changes.</param>
    /// <param name="actor">The person enabling or disabling the skill.</param>
    Task LogSkillChangedAsync(
        Skill skill,
        SkillVersion priorVersion,
        User actor);

    /// <summary>
    /// Logs a skill run.
    /// </summary>
    /// <param name="skill">The skill that ran.</param>
    /// <param name="arguments">The arguments passed to the skill.</param>
    /// <param name="pattern">The pattern that caused to skill to be run, if any.</param>
    /// <param name="signal">The signal that this skill was responding to, if any.</param>
    /// <param name="actor">The user that invoked the skill.</param>
    /// <param name="room">The room the skill ran in.</param>
    /// <param name="response">The response to running the skill.</param>
    /// <param name="auditId">The Id for this audit entry.</param>
    /// <param name="parentAuditId">The audit entry for the skill that raised a signal to call this skill.</param>
    /// <param name="auditProperties">Additional Audit Log properties to include in the event.</param>
    Task<SkillRunAuditEvent> LogSkillRunAsync(
        Skill skill,
        IArguments arguments,
        IPattern? pattern,
        string? signal,
        User actor,
        PlatformRoom room,
        SkillRunResponse response,
        Guid auditId,
        Guid? parentAuditId,
        SkillRunProperties? auditProperties = null);

    /// <summary>
    /// Logs that a skill ran unsuccessfully and threw an exception.
    /// </summary>
    /// <param name="skill">The skill that ran.</param>
    /// <param name="arguments">The arguments passed to the skill.</param>
    /// <param name="pattern">The pattern that caused to skill to be run, if any.</param>
    /// <param name="signal">The signal that this skill was responding to, if any.</param>
    /// <param name="actor">The user that invoked the skill.</param>
    /// <param name="room">The room the skill ran in.</param>
    /// <param name="exception">The exception thrown by the skill.</param>
    /// <param name="auditId">The Id for this audit entry.</param>
    /// <param name="parentAuditId">The audit entry for the skill that raised a signal to call this skill.</param>
    /// <param name="includeExceptionDetails">Indicates if a detailed error message should be provided.</param>
    /// <param name="auditProperties">Additional Audit Log properties to include in the event.</param>
    Task<SkillRunAuditEvent> LogSkillRunAsync(
        Skill skill,
        IArguments arguments,
        IPattern? pattern,
        string? signal,
        User actor,
        PlatformRoom room,
        Exception exception,
        Guid auditId,
        Guid? parentAuditId,
        bool includeExceptionDetails,
        SkillRunProperties? auditProperties = null);

    /// <summary>
    /// Logs when a skill is called by an HTTP trigger.
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    /// <param name="triggerEvent">The HTTP trigger request.</param>
    /// <param name="responseHeaders">The response headers set by the skill.</param>
    /// <param name="result">The content result in response to the trigger.</param>
    /// <param name="auditId">The Id for this audit entry.</param>
    Task<HttpTriggerRunEvent> LogHttpTriggerRunEventAsync(
        SkillHttpTrigger trigger,
        HttpTriggerRequest triggerEvent,
        IReadOnlyDictionary<string, string?[]> responseHeaders,
        ContentResult result,
        Guid auditId);

    /// <summary>
    /// Logs when a skill is called by <see cref="Playbook"/> action.
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    /// <param name="response">The response of running the trigger.</param>
    /// <param name="auditId">The Id for this audit entry.</param>
    Task<PlaybookActionSkillRunEvent> LogPlaybookActionTriggerRunEventAsync(
        SkillPlaybookActionTrigger trigger,
        SkillRunResponse response,
        Guid auditId);

    /// <summary>
    /// Logs when a skill is called by a scheduled trigger.
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    /// <param name="response">The response of running the trigger.</param>
    /// <param name="auditId">The Id for this audit entry.</param>
    Task<ScheduledTriggerRunEvent> LogScheduledTriggerRunEventAsync(
        SkillScheduledTrigger trigger,
        SkillRunResponse response,
        Guid auditId);
}

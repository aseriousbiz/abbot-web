using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Telemetry;

/// <summary>
/// Skill specific audit log.
/// </summary>
public class SkillAuditLog : ISkillAuditLog
{
    readonly AbbotContext _db;
    readonly IUserRepository _userRepository;

    /// <summary>
    /// Constructs a SkillAuditLog
    /// </summary>
    /// <param name="db">The database context.</param>
    public SkillAuditLog(AbbotContext db, IUserRepository userRepository)
    {
        _db = db;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public Task<SkillRunAuditEvent> LogSkillRunAsync(
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
        SkillRunProperties? auditProperties = null)
    {
        var error = includeExceptionDetails
            ? exception.Unwrap().ToString()
            : exception.Message;
        return CreateSkillRunAuditEventAsync(
            skill,
            arguments,
            pattern,
            actor,
            room,
            error,
            auditId,
            parentAuditId,
            signal,
            auditProperties);
    }

    /// <inheritdoc />
    public Task<SkillRunAuditEvent> LogSkillRunAsync(
        Skill skill,
        IArguments arguments,
        IPattern? pattern,
        string? signal,
        User actor,
        PlatformRoom room,
        SkillRunResponse response,
        Guid auditId,
        Guid? parentAuditId,
        SkillRunProperties? auditProperties = null)
    {
        var error = response.Success || response.Errors is null
            ? null
            : string.Join("\n", response.Errors.Select(e => e.Description + "\n\n" + e.StackTrace));

        return CreateSkillRunAuditEventAsync(
            skill,
            arguments,
            pattern,
            actor,
            room,
            error,
            auditId,
            parentAuditId,
            signal,
            auditProperties);
    }

    public async Task<HttpTriggerRunEvent> LogHttpTriggerRunEventAsync(
        SkillHttpTrigger trigger,
        HttpTriggerRequest triggerEvent,
        IReadOnlyDictionary<string, string?[]> responseHeaders,
        ContentResult result,
        Guid auditId)
    {
        var (actor, _) = await _userRepository.EnsureAbbotMemberAsync(trigger.Skill.Organization);
        var auditEvent = CreateTriggerRunEventInstance<HttpTriggerRunEvent>(
            trigger,
            $"Ran `{trigger.Skill.Name}` via HTTP trigger.",
            actor,
            auditId);
        auditEvent.Headers = string.Join("\n", triggerEvent.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"));
        auditEvent.Url = triggerEvent.Url;
        auditEvent.StatusCode = result.StatusCode ?? 0;
        auditEvent.Code = trigger.Skill.Code;
        auditEvent.Arguments = triggerEvent.RawBody ?? string.Empty;
        auditEvent.Room = trigger.Name;
        auditEvent.RoomId = trigger.RoomId;
        auditEvent.ResponseHeaders =
            string.Join("\n", responseHeaders.Select(h => $"{h.Key}={string.Join(",", h.Value)}"));
        auditEvent.ResponseContentType = result.ContentType;

        if (result.StatusCode == 500)
        {
            auditEvent.ErrorMessage = result.Content;
        }
        else
        {
            auditEvent.Response = result.Content ?? string.Empty;
        }

        return await AddAndSaveAuditEvent(auditEvent, trigger.Skill.Organization);
    }

    object InitializeAuditEvent(HttpTriggerRunEvent httpTriggerRunEvent)
    {
        throw new NotImplementedException();
    }

    public async Task<PlaybookActionSkillRunEvent> LogPlaybookActionTriggerRunEventAsync(
        SkillPlaybookActionTrigger trigger,
        SkillRunResponse response,
        Guid auditId)
    {
        var (actor, _) = await _userRepository.EnsureAbbotMemberAsync(trigger.Skill.Organization);
        var auditEvent = CreateTriggerRunEventInstance<PlaybookActionSkillRunEvent>(
            trigger,
            $"Ran `{trigger.Skill.Name}` via action in playbook `{trigger.PlaybookRun.Playbook.Slug}`.",
            actor,
            auditId);
        auditEvent.Code = trigger.Skill.Code;
        auditEvent.Arguments = trigger.Arguments ?? string.Empty;
        auditEvent.Room = trigger.Name;
        auditEvent.RoomId = trigger.RoomId;

        if (!response.Success)
        {
            auditEvent.ErrorMessage = AbbotJsonFormat.Default.Serialize(response.Errors, true);
        }

        return await AddAndSaveAuditEvent(auditEvent, trigger.Skill.Organization);
    }

    public async Task<ScheduledTriggerRunEvent> LogScheduledTriggerRunEventAsync(
        SkillScheduledTrigger trigger,
        SkillRunResponse response,
        Guid auditId)
    {
        var (actor, _) = await _userRepository.EnsureAbbotMemberAsync(trigger.Skill.Organization);
        var auditEvent = CreateTriggerRunEventInstance<ScheduledTriggerRunEvent>(
            trigger,
            $"Ran `{trigger.Skill.Name}` via scheduled trigger.",
            actor,
            auditId);
        auditEvent.Code = trigger.Skill.Code;
        auditEvent.Arguments = trigger.Arguments ?? string.Empty;
        auditEvent.Room = trigger.Name;
        auditEvent.RoomId = trigger.RoomId;

        if (!response.Success)
        {
            auditEvent.ErrorMessage = AbbotJsonFormat.Default.Serialize(response.Errors, true);
        }

        return await AddAndSaveAuditEvent(auditEvent, trigger.Skill.Organization);
    }

    /// <summary>
    /// Logs that a skill was enabled or disabled.
    /// </summary>
    /// <param name="skill">The skill changed.</param>
    /// <param name="actor">The person enabling or disabling the skill.</param>
    public Task<SkillInfoChangedAuditEvent> LogSkillEnabledChangedAsync(Skill skill, User actor)
    {
        var verb = skill.Enabled ? "Enabled" : "Disabled";
        return CreateSkillInfoChanged(skill, verb, string.Empty, actor);
    }

    public async Task LogSkillChangedAsync(
        Skill skill,
        SkillVersion priorVersion,
        User actor)
    {
        if (priorVersion.Code is not null)
        {
            await LogSkillCodeEditSessionEventAsync(skill, priorVersion, actor);
        }

        // Get properties other than Code.
        var propertiesChanged = priorVersion
            .ChangedProperties
            .Where(property => !property.Equals(nameof(priorVersion.Code), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (propertiesChanged.Any())
        {
            await LogSkillInfoChangedAsync(skill, priorVersion, propertiesChanged, actor);
        }
    }

    async Task LogSkillInfoChangedAsync(
        Skill skill,
        SkillVersion priorVersion,
        IList<string> propertiesChanged,
        User actor)
    {
        if (propertiesChanged.Contains(nameof(Skill.Name), StringComparer.Ordinal))
        {
            await LogRenameAsync(skill, priorVersion, actor);
        }

        if (propertiesChanged.Contains(nameof(Skill.Restricted), StringComparer.Ordinal))
        {
            await LogRestrictedChangedAsync(skill, actor);
        }

        var remainingProperties = propertiesChanged
            .Where(p => !p.Equals(nameof(Skill.Name), StringComparison.Ordinal)
                        && !p.Equals(nameof(Skill.Restricted), StringComparison.Ordinal))
            .ToList();

        if (remainingProperties.Any())
        {
            var propertyList = remainingProperties.Select(item => $"`{item}`").Humanize();
            var propertyLabel = remainingProperties is { Count: 1 } ? "property" : "properties";
            // Log property changes
            var auditEvent = CreateSkillAuditEventInstance<SkillInfoChangedAuditEvent>(
                skill,
                $"Changed {propertyLabel} {propertyList} of skill `{skill.Name}`.",
                actor);
            if (remainingProperties.Contains(nameof(skill.Description), StringComparer.Ordinal))
            {
                auditEvent.NewDescription = skill.Description;
            }
            if (remainingProperties.Contains("Usage", StringComparer.Ordinal))
            {
                auditEvent.NewUsage = skill.UsageText;
            }
            auditEvent.ChangeDescription = skill.Description;
            auditEvent.ChangeType = "Changed";
            auditEvent.ChangeDescription = string.Join(',', remainingProperties);

            await _db.AuditEvents.AddAsync(auditEvent);
        }

        await _db.SaveChangesAsync();
    }

    async Task LogRenameAsync(Skill skill, SkillVersion priorVersion, User actor)
    {
        // Log Rename
        var auditEvent = CreateSkillAuditEventInstance<SkillInfoChangedAuditEvent>(
            skill,
            $"Renamed skill `{priorVersion.Name}` to `{skill.Name}`.",
            actor);
        auditEvent.ChangeType = "Renamed";
        auditEvent.ChangeDescription = priorVersion.Name ?? string.Empty;
        await _db.AuditEvents.AddAsync(auditEvent);
    }

    async Task LogRestrictedChangedAsync(Skill skill, User actor)
    {
        var verb = skill.Restricted ? "Restricted" : "Unrestricted";
        // Log Rename
        var auditEvent = CreateSkillAuditEventInstance<SkillInfoChangedAuditEvent>(
            skill,
            $"{verb} skill `{skill.Name}`.",
            actor);
        auditEvent.ChangeType = verb;
        auditEvent.ChangeDescription = string.Empty;
        await _db.AuditEvents.AddAsync(auditEvent);
    }

    /// <summary>
    /// Logs a skill code edit session. This only applies when the code for a skill changes.
    /// </summary>
    /// <param name="skill">The skill being edited.</param>
    /// <param name="priorVersion">The version of the skill prior to the latest changes.</param>
    /// <param name="actor">The user making the change.</param>
    async Task LogSkillCodeEditSessionEventAsync(
        Skill skill,
        SkillVersion priorVersion,
        User actor)
    {
        var tenMinutesAgo = priorVersion.Created.AddMinutes(-10);
        var currentSession = await _db.AuditEvents.OfType<SkillEditSessionAuditEvent>()
            .Where(e => e.SkillId == priorVersion.SkillId && e.ActorId == actor.Id && e.Modified > tenMinutesAgo)
            .OrderByDescending(e => e.Modified)
            .FirstOrDefaultAsync();

        if (currentSession is null)
        {
            // Create a new edit session.
            currentSession = CreateSkillAuditEventInstance<SkillEditSessionAuditEvent>(
                skill,
                string.Empty,
                actor);
            currentSession.Created = priorVersion.Created;
            currentSession.FirstSkillVersionId = priorVersion.Id;

            await _db.AuditEvents.AddAsync(currentSession);
        }
        currentSession.Code = skill.Code;
        currentSession.Modified = priorVersion.Created;
        currentSession.EditCount++;
        currentSession.Description = GetDescription(currentSession, skill);

        await _db.SaveChangesAsync();
    }

    static string GetDescription(SkillEditSessionAuditEvent session, Skill skill)
    {
        return session.EditCount is 1
            ? $"Edited {skill.Language.Humanize()} Code of skill `{skill.Name}`."
            : $"Edited {skill.Language.Humanize()} Code of skill `{skill.Name}` {session.EditCount} times for a span of `{(session.Modified - session.Created).FormatDuration()}`.";
    }

    async Task<SkillRunAuditEvent> CreateSkillRunAuditEventAsync(
        Skill skill,
        IArguments arguments,
        IPattern? pattern,
        User actor,
        PlatformRoom room,
        string? errorText,
        Guid? auditId,
        Guid? parentAuditId = null,
        string? signal = null,
        SkillRunProperties? auditProperties = null)
    {
        // When we call a custom skill, the MessageContext.Arguments property are the arguments for the built-in
        // RemoteSkillCallSkill, and thus includes the name of the skill. We need to pull out the arguments for
        // the remote skill when we log them, hence the following line of code.
        var command = $"`{skill.Name}{(arguments is { Count: 0 } ? "` with no arguments" : $" {arguments}`")}";

        var displayRoom = room.ToAuditLogString();
        var description = signal is not null
            ? $"Ran skill `{skill.Name}` in {displayRoom} in response to the signal `{signal}`."
            : pattern is null
                ? $"Ran command {command} in {displayRoom}."
                : $"Ran skill `{skill.Name}` in {displayRoom} due to pattern `{pattern.Name}`.";

        var skillRunEvent = CreateSkillAuditEventInstance<SkillRunAuditEvent>(
            skill,
            description,
            actor,
            auditId,
            parentAuditId);
        skillRunEvent.Signal = signal;
        skillRunEvent.Code = skill.Code;
        skillRunEvent.Arguments = arguments.Value;
        skillRunEvent.ErrorMessage = errorText;
        skillRunEvent.Room = room.Name;
        skillRunEvent.RoomId = room.Id;
        skillRunEvent.PatternDescription = pattern?.Description;
        skillRunEvent.Properties = auditProperties ?? new();

        if (skill.Secrets.Any())
        {
            skillRunEvent.Secrets = $"`{skill.Secrets.Count.ToQuantity("secret", "secrets")}`: "
                                    + string.Join(", ", skill.Secrets.Humanize(s => $"`{s.Name}`"));
            skillRunEvent.Description += $" which contains {skillRunEvent.Secrets}";
        }

        return await AddAndSaveAuditEvent(skillRunEvent, skill.Organization);
    }

    async Task<SkillInfoChangedAuditEvent> CreateSkillInfoChanged(
        Skill skill,
        string changeType,
        string changeDescription,
        User actor)
    {
        var descriptionAppendage = changeDescription.Length > 0 ? $" {changeDescription}" : string.Empty;
        var skillChangedEvent = CreateSkillAuditEventInstance<SkillInfoChangedAuditEvent>(
            skill,
            $"{changeType} {skill.Language.Humanize()} skill `{skill.Name}`{descriptionAppendage}.",
            actor);

        skillChangedEvent.ChangeDescription = changeDescription;
        skillChangedEvent.ChangeType = changeType;

        return await AddAndSaveAuditEvent(skillChangedEvent, skill.Organization);
    }

    static TAuditEvent CreateSkillAuditEventInstance<TAuditEvent>(
        Skill skill,
        string description,
        User actor,
        Guid? auditId = null,
        Guid? parentAuditId = null) where TAuditEvent : SkillAuditEvent, new()
    {
        return new()
        {
            Identifier = auditId ?? Guid.NewGuid(),
            ParentIdentifier = parentAuditId,
            EntityId = skill.Id,
            SkillId = skill.Id,
            SkillName = skill.Name,
            Language = skill.Language,
            Actor = actor,
            Organization = skill.Organization,
            Description = description,
            TraceId = Activity.Current?.Id,
        };
    }

    static TAuditEvent CreateTriggerRunEventInstance<TAuditEvent>(
        SkillTrigger trigger,
        string description,
        User actor,
        Guid auditId)
        where TAuditEvent : TriggerRunEvent, new()
    {
        var auditEvent = CreateSkillAuditEventInstance<TAuditEvent>(trigger.Skill, description, actor, auditId);
        auditEvent.EntityId = trigger.Id;
        auditEvent.TriggerDescription = trigger.Description ?? string.Empty;
        return auditEvent;
    }

    async Task<TAuditEvent> AddAndSaveAuditEvent<TAuditEvent>(TAuditEvent auditEvent, IOrganizationIdentifier organization)
        where TAuditEvent : AuditEventBase
    {
        await _db.AuditEvents.AddAsync(auditEvent);
        await _db.SaveChangesAsync();
        return auditEvent;
    }
}

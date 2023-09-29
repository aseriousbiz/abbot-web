using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Messages;

/// <summary>
/// Client to the Abbot skill runners which are Azure Functions that actually
/// execute the skill code.
/// </summary>
public interface ISkillRunnerClient
{
    /// <summary>
    /// Sends a skill invocation request to the skill runner. This is called when Abbot receives a message
    /// from Slack and the message is a request to call a user-defined skill.
    /// </summary>
    /// <param name="skill">The skill to run.</param>
    /// <param name="arguments">The tokenized arguments to the skill.</param>
    /// <param name="commandText">The command that invoked the skill.</param>
    /// <param name="mentions">The list of mentioned users.</param>
    /// <param name="sender">The member calling the skill.</param>
    /// <param name="bot">The abbot bot user.</param>
    /// <param name="platformRoom">The platform room the skill is responding to.</param>
    /// <param name="customer">The customer associated with the room, if any.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="isInteraction">Whether this call is an interaction.</param>
    /// <param name="pattern">The <see cref="IPattern"/> that matched, if any, to trigger this skill call.</param>
    /// <param name="signal">Information about the signal that's causing this skill to be called.</param>
    /// <param name="messageUrl">The URL of the message that triggered this skill, if any.</param>
    /// <param name="messageId">The platform-specific ID of the message that triggered this skill, if any.</param>
    /// <param name="threadId">The platform-specific Id of the thread the message that triggered the skill was in, if any.</param>
    /// <param name="triggeringMessageAuthor">The <see cref="Member"/> that authored the message that triggered this call. If this is in response to an interaction such as a reaction or button press, this is the author of the source message.</param>
    /// <param name="conversation">The <see cref="ChatConversation"/> in which the skill was run, if any.</param>
    /// <param name="room">The Abbot <see cref="Room"/> entity in which the skill was run, if any.</param>
    /// <param name="interactionInfo">The interaction info for the message or event.</param>
    /// <param name="passiveReplies">If <c>true</c>, then passive replies are expected.</param>
    /// <param name="auditProperties">Additional Audit Log properties to include in the skill run audit event.</param>
    Task<SkillRunResponse> SendAsync(
        Skill skill,
        IArguments arguments,
        string commandText,
        IEnumerable<Member> mentions,
        Member sender,
        BotChannelUser bot,
        PlatformRoom platformRoom,
        CustomerInfo? customer,
        Uri skillUrl,
        bool isInteraction = false,
        IPattern? pattern = null,
        SignalMessage? signal = null,
        Uri? messageUrl = null,
        string? messageId = null,
        string? threadId = null,
        Member? triggeringMessageAuthor = null,
        ChatConversation? conversation = null,
        Room? room = null,
        MessageInteractionInfo? interactionInfo = null,
        bool passiveReplies = false,
        SkillRunProperties? auditProperties = null);

    /// <summary>
    /// Forwards an HttpTrigger event to a skill.
    /// </summary>
    /// <param name="trigger">The Http trigger to invoke.</param>
    /// <param name="triggerRequest">The HTTP request that caused the trigger.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="auditId">The Id for the audit log entry for this skill run.</param>
    Task<SkillRunResponse> SendHttpTriggerAsync(
        SkillHttpTrigger trigger,
        HttpTriggerRequest triggerRequest,
        Uri skillUrl,
        Guid auditId);

    /// <summary>
    /// Forwards a <see cref="Playbook"/> action to a skill.
    /// </summary>
    /// <param name="trigger">The <see cref="Playbook"/> action trigger to invoke.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="auditId">The Id for the audit log entry for this skill run.</param>
    Task<SkillRunResponse> SendPlaybookActionTriggerAsync(SkillPlaybookActionTrigger trigger, Uri skillUrl, Guid auditId);

    /// <summary>
    /// Forwards a scheduled trigger event to a skill.
    /// </summary>
    /// <param name="trigger">The scheduled trigger to invoke.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="auditId">The Id for the audit log entry for this skill run.</param>
    Task<SkillRunResponse> SendScheduledTriggerAsync(SkillScheduledTrigger trigger, Uri skillUrl, Guid auditId);
}

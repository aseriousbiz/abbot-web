using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Skills;
using Stripe;

namespace Serious.Abbot.Telemetry;

/// <summary>
/// A log of important events that occur for an organization.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Logs that an organization was deleted.
    /// </summary>
    /// <param name="organization">The organization instance that was deleted.</param>
    /// <param name="reason">The reason it was deleted, if any.</param>
    /// <param name="staffActor">The Staff <see cref="Member"/> that deleted the organization.</param>
    Task LogOrganizationDeleted(Organization organization, string reason, Member staffActor);

    /// <summary>
    /// Logs that an entity was created.
    /// </summary>
    /// <param name="entity">The entity that was created.</param>
    /// <param name="actor">The user that created the entity.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task<AuditEventBase?> LogEntityCreatedAsync(IAuditableEntity entity, User actor, Organization organization);

    /// <summary>
    /// Logs that an entity was deleted.
    /// </summary>
    /// <param name="entity">The entity that was created.</param>
    /// <param name="actor">The user that deleted the entity.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task<AuditEventBase?> LogEntityDeletedAsync(IAuditableEntity entity, User actor, Organization organization);

    /// <summary>
    /// Logs that an entity was modified.
    /// </summary>
    /// <param name="entity">The entity that was modified.</param>
    /// <param name="actor">The user that modified the entity.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task<AuditEventBase?> LogEntityChangedAsync(IAuditableEntity entity, User actor, Organization organization);

    /// <summary>
    /// Log an event when a staff member has to view the code for a customer audit event.
    /// </summary>
    /// <param name="viewedAuditEvent">The audit event that the staff member viewed.</param>
    /// <param name="reason">The reason the staff member viewed the code for the event.</param>
    /// <param name="actor">The user that deleted the entity.</param>
    Task LogStaffViewedCodeEventAsync(AuditEventBase viewedAuditEvent, string reason, User actor);

    /// <summary>
    /// Log an event when a staff member has to view a slack event for a customer.
    /// </summary>
    /// <param name="slackEvent">The slack event that the staff member viewed.</param>
    /// <param name="reason">The reason the staff member viewed the code for the event.</param>
    /// <param name="actor">The user that deleted the entity.</param>
    Task LogStaffViewedSlackEventAsync(SlackEvent slackEvent, string reason, User actor);

    /// <summary>
    /// Log that the <paramref name="actor" /> installed Abbot to the chat platform for the
    /// <paramref name="organization" />.
    /// </summary>
    /// <param name="info">Details about the app that was uninstalled.</param>
    /// <param name="organization">The organization where Abbot was added.</param>
    /// <param name="actor">The <see cref="Member"/> that added Abbot.</param>
    Task LogInstalledAbbotAsync(InstallationInfo info, Organization organization, Member actor);

    /// <summary>
    /// Log that the <paramref name="actor" /> uninstalled Abbot from the chat platform for the
    /// <paramref name="organization" />.
    /// </summary>
    /// <param name="info">Details about the app that was uninstalled.</param>
    /// <param name="organization">The organization where Abbot was removed.</param>
    /// <param name="actor">The <see cref="Member"/> that removed Abbot.</param>
    Task LogUninstalledAbbotAsync(InstallationInfo info, Organization organization, Member actor);

    /// <summary>
    /// Logs a generic administrative activity.
    /// </summary>
    /// <param name="description">A description of the activity.</param>
    /// <param name="actor">The admin user that took the activity.</param>
    /// <param name="organization">The organization.</param>
    Task LogAdminActivityAsync(string description, User actor, Organization organization);

    Task LogDenyUser(Member subject, User actor);

    Task LogArchiveUserAsync(Member subject, User actor);

    Task LogRestoreUserAsync(Member subject, User actor);

    Task LogAutoApproveChanged(User actor, Organization organization);

    Task LogApiEnabledChanged(User actor, Organization organization);

    Task LogDefaultChatResponderEnabledChanged(User actor, Organization organization);

    Task LogOrganizationShortcutChanged(User actor, char oldShortcut, Organization organization);

    Task LogOrganizationAvatarChanged(User actor, string avatarType, Organization organization);

    Task LogManagedConversationsEnabledAsync(Member actor, Room room, Organization organization);

    Task LogManagedConversationsDisabledAsync(Member actor, Room room, Organization organization);

    /// <summary>
    /// Log that the package was published.
    /// </summary>
    /// <param name="package">The published package.</param>
    /// <param name="actor">The user that published the package.</param>
    /// <param name="organization">The organization that owns the package.</param>
    Task LogPackagePublishedAsync(Package package, User actor, Organization organization);

    /// <summary>
    /// Log that the package was unlisted.
    /// </summary>
    /// <param name="package">The unlisted package.</param>
    /// <param name="actor">The user that unlisted the package.</param>
    /// <param name="organization">The organization that owns the package.</param>
    Task LogPackageUnlistedAsync(Package package, User actor, Organization organization);

    /// <summary>
    /// Log that the package details (README) was updated.
    /// </summary>
    /// <param name="package">The published package.</param>
    /// <param name="actor">The user that published the package.</param>
    /// <param name="organization">The organization that owns the package.</param>
    Task LogPackageChangedAsync(Package package, User actor, Organization organization);

    /// <summary>
    /// Logs when a skill is not found.
    /// </summary>
    /// <param name="command">The command the user sent to Abbot.</param>
    /// <param name="response">The response sent to the user.</param>
    /// <param name="responseSource">The source of the response sent to the user.</param>
    /// <param name="actor">The actor making the change.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task LogSkillNotFoundAsync(
        string command,
        string response,
        ResponseSource responseSource,
        User actor,
        Organization organization);

    /// <summary>
    /// Logs when someone links a conversation to an external resource.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="linkType">The type of the link.</param>
    /// <param name="externalId">The external ID that the conversation is linked to.</param>
    /// <param name="actor">The actor making the change.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task LogConversationLinkedAsync(
        Conversation conversation,
        ConversationLinkType linkType,
        string externalId,
        Member actor,
        Organization organization);

    /// <summary>
    /// Logs when someone changes an assignment of an agent to or from a conversation.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="assignees">The <see cref="Member"/> the conversation the assignment is for.</param>
    /// <param name="actor">The actor making the change.</param>
    Task LogConversationAssignmentAsync(
        Conversation conversation,
        IReadOnlyList<Member> assignees,
        Member actor);

    /// <summary>
    /// Logs when a <see cref="Conversation"/> is attached to a <see cref="Hub"/>
    /// </summary>
    /// <param name="conversation">The conversation</param>
    /// <param name="hub">The hub to which the conversation was attached</param>
    /// <param name="hubThreadId">The platform-specific ID of the message representing the Hub Thread for this conversation.</param>
    /// <param name="actor">The actor making the change.</param>
    Task LogConversationAttachedAsync(Conversation conversation, Hub hub, string hubThreadId, Member actor);

    /// <summary>
    /// Logs when someone invites Abbot (or custom) to a room.
    /// </summary>
    /// <param name="actor">The actor making the change.</param>
    /// <param name="room">The room.</param>
    Task LogBotInvitedAsync(Member actor, Room room);

    /// <summary>
    /// Logs when someone changes the response times for the room.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="oldTarget">The old target.</param>
    /// <param name="oldDeadline">The old deadline.</param>
    /// <param name="actor">The actor making the change.</param>
    Task LogRoomResponseTimesChangedAsync(
        Room room,
        TimeSpan? oldTarget,
        TimeSpan? oldDeadline,
        Member actor);

    /// <summary>
    /// Logs when someone changes the responders for the room.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="roomRole">The room role the responders were added to/removed from.</param>
    /// <param name="addedResponders">The responders added as to the <paramref name="roomRole"/> for the <paramref name="room"/>.</param>
    /// <param name="removedResponders"></param>
    /// <param name="respondersCount">The current number of responders after this change.</param>
    /// <param name="actor">The actor making the change.</param>
    Task LogRoomRespondersChangedAsync(
        Room room,
        RoomRole roomRole,
        IReadOnlyList<Member> addedResponders,
        IReadOnlyList<Member> removedResponders,
        int respondersCount,
        Member actor);

    /// <summary>
    /// Logs when someone links a room to an external resource.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="linkType">The type of the link.</param>
    /// <param name="externalId">The external ID that the room is linked to.</param>
    /// <param name="displayName">The display name of the resource the room is linked to.</param>
    /// <param name="actor">The actor making the change.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task LogRoomLinkedAsync(
        Room room,
        RoomLinkType linkType,
        string externalId,
        string displayName,
        Member actor,
        Organization organization);

    /// <summary>
    /// Logs when someone unlinks a room from an external resource.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="linkType">The type of the link.</param>
    /// <param name="externalId">The external ID that the room is no longer linked to.</param>
    /// <param name="displayName">The display name of the resource the room is no longer linked to.</param>
    /// <param name="actor">The actor making the change.</param>
    /// <param name="organization">The organization in which this event occurred.</param>
    Task LogRoomUnlinkedAsync(
        Room room,
        RoomLinkType linkType,
        string externalId,
        string displayName,
        Member actor,
        Organization organization);

    /// <summary>
    /// Log the purchase of a plan.
    /// </summary>
    /// <param name="subscription">The subscription purchased.</param>
    /// <param name="actor">The user that purchased it.</param>
    /// <param name="organization">The organization that was purchased.</param>
    Task LogPurchaseSubscriptionAsync(Subscription subscription, Member actor, Organization organization);

    /// <summary>
    /// Log the purchase of a plan.
    /// </summary>
    /// <param name="previousPlan">The previous plan.</param>
    /// <param name="subscription">The subscription cancelled.</param>
    /// <param name="actor">The user that purchased it.</param>
    /// <param name="organization">The organization that was purchased.</param>
    Task LogCancelSubscriptionAsync(PlanType previousPlan, Subscription subscription, User actor, Organization organization);

    /// <summary>
    /// Log the changing of the plans.
    /// </summary>
    /// <param name="previousPlan">The previous plan.</param>
    /// <param name="subscription">The subscription cancelled.</param>
    /// <param name="actor">The user that purchased it.</param>
    /// <param name="organization">The organization that was purchased.</param>
    Task LogChangeSubscriptionAsync(PlanType previousPlan, Subscription subscription, User actor, Organization organization);

    /// <summary>
    /// Logs that a user set a property via the `my` skill.
    /// </summary>
    Task LogUserPropertyChangedAsync(string property, string value, Member actor, Organization organization);

    /// <summary>
    /// Logs that a built-in skill ran successfully.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <param name="messageContext">The message associated with running the skill.</param>
    Task<BuiltInSkillRunEvent> LogBuiltInSkillRunAsync(ISkill skill, MessageContext messageContext);

    /// <summary>
    /// Logs that a built-in skill ran unsuccessfully and threw an exception.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <param name="messageContext">The message associated with running the skill.</param>
    /// <param name="exception">The exception thrown by the skill.</param>
    Task<BuiltInSkillRunEvent> LogBuiltInSkillRunAsync(ISkill skill, MessageContext messageContext, Exception exception);

    /// <summary>
    /// Log a generic audit event
    /// </summary>
    /// <param name="auditEventBuilder">The <see cref="AuditEventBuilder"/> describing the event to log.</param>
    /// <returns>The logged <see cref="AuditEvent"/>.</returns>
    Task<AuditEvent> LogAuditEventAsync(AuditEventBuilder auditEventBuilder);

    /// <summary>
    /// Log a generic audit event and an analytics event
    /// </summary>
    /// <param name="auditEventBuilder">The <see cref="AuditEventBuilder"/> describing the event to log.</param>
    /// <param name="analyticsEventBuilder">The <see cref="AnalyticsEventBuilder"/> describing the analytics event to log.</param>
    /// <returns></returns>
    Task<AuditEvent> LogAuditEventAsync(AuditEventBuilder auditEventBuilder, AnalyticsEventBuilder analyticsEventBuilder);

    /// <summary>
    /// Logs that an organization has started a trial.
    /// </summary>
    /// <param name="actor">The member that triggered the event.</param>
    /// <param name="organization">The organization this event occurred in.</param>
    /// <param name="trialPlan">The trial plan.</param>
    Task LogTrialStartedAsync(Member actor, Organization organization, TrialPlan trialPlan);

    /// <summary>
    /// Logs an Integration's new <see cref="Integration.Enabled"/> status.
    /// </summary>
    /// <param name="integration">The integration.</param>
    /// <param name="actor">The user that changed it.</param>
    /// <returns></returns>
    Task LogIntegrationStatusChangedAsync(Integration integration, Member actor);

    /// <summary>
    /// Logs an event related to a <see cref="Form"/>
    /// </summary>
    /// <param name="actor">The <see cref="Member"/> who performed the action.</param>
    /// <param name="form">The <see cref="Form"/> the action was performed on.</param>
    /// <param name="action">The action taken, in a format suitable for use as an event for <see cref="Segment.IAnalyticsClient"/>.</param>
    /// <param name="description">A description of the action taken.</param>
    Task LogFormEventAsync(Member actor, Form form, string action, string description);

    /// <summary>
    /// Logs an event related to a <see cref="Hub"/>
    /// </summary>
    /// <param name="actor">The <see cref="Member"/> who performed the action.</param>
    /// <param name="hub">The <see cref="Hub"/> the action was performed on.</param>
    /// <param name="action">The action taken, in a format suitable for use as an event for <see cref="Segment.IAnalyticsClient"/>.</param>
    /// <param name="description">A description of the action taken.</param>
    /// <param name="properties">Additional properties associated with the event.</param>
    Task LogHubEventAsync(Member actor, Hub hub, string action, string description, HubAuditEventProperties? properties = null);
}

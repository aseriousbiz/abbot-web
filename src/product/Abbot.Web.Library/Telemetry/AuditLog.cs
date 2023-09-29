using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Skills;
using Serious.Text;
using Stripe;

namespace Serious.Abbot.Telemetry;

/// <summary>
/// A log of important events that occur for an organization.
/// </summary>
public class AuditLog : IAuditLog
{
    readonly AbbotContext _db;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public AuditLog(AbbotContext db, IAnalyticsClient analyticsClient, IClock clock)
    {
        _db = db;
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    public async Task LogOrganizationDeleted(Organization organization, string reason, Member staffActor)
    {
        await LogAuditEventAsync(new()
        {
            Type = new AuditEventType("Organization", AuditOperation.Removed),
            Description = $"Deleted {organization.Name} ({organization.PlatformId}) organization.",
            Actor = staffActor,
            Organization = staffActor.Organization,
            EntityId = organization.Id,
            StaffPerformed = true,
            StaffReason = reason,
        });
    }

    public async Task<AuditEventBase?> LogEntityCreatedAsync(IAuditableEntity entity, User actor, Organization organization)
        => await LogEntityAuditEventAsync(entity, AuditOperation.Created, actor, organization);

    public async Task<AuditEventBase?> LogEntityDeletedAsync(IAuditableEntity entity, User actor, Organization organization)
        => await LogEntityAuditEventAsync(entity, AuditOperation.Removed, actor, organization);

    public async Task<AuditEventBase?> LogEntityChangedAsync(IAuditableEntity entity, User actor, Organization organization)
        => await LogEntityAuditEventAsync(entity, AuditOperation.Changed, actor, organization);

    async Task<AuditEventBase?> LogEntityAuditEventAsync(
        IAuditableEntity entity,
        AuditOperation auditOperation,
        User actor,
        Organization organization)
    {
        var auditEvent = CreateAuditEventFromAuditableEntity(entity, auditOperation);
        if (auditEvent is null)
        {
            return null;
        }
#pragma warning disable CS0618
        return await SaveAuditEventAsync(auditEvent, actor, organization);
#pragma warning restore CS0618
    }

    static AuditEventBase? CreateAuditEventFromAuditableEntity(IAuditableEntity entity, AuditOperation auditOperation)
    {
        var auditEvent = entity.CreateAuditEventInstance(auditOperation);
        if (auditEvent is null)
        {
            return null;
        }

        auditEvent.EntityId = entity.Id;
        if (auditEvent is SkillAuditEvent skillEvent)
        {
            var skill = entity switch
            {
                Skill skillEntity => skillEntity,
                ISkillChildEntity child => child.Skill,
                _ => null
            };
            if (skill is not null)
            {
                skillEvent.Language = skill.Language;
                skillEvent.SkillId = skill.Id;
                skillEvent.SkillName = skill.Name;
            }
        }

        return auditEvent;
    }

    public async Task LogInstalledAbbotAsync(InstallationInfo info, Organization organization, Member actor)
    {
        await SaveAuditEventAsync(
            new InstallationEvent
            {
                Description = $"Installed {info.AppName ?? "Abbot"} to the {organization.Name} {info.PlatformType}.",
                Properties = info,
            },
            actor, organization);

        _analyticsClient.Track("Slack App Installed", AnalyticsFeature.Activations, actor, organization,
            new {
                app_id = info.AppId,
            });
    }

    public async Task LogUninstalledAbbotAsync(InstallationInfo info, Organization organization, Member actor)
    {
        await SaveAuditEventAsync(
            new InstallationEvent
            {
                Description = $"Uninstalled {info.AppName ?? "Abbot"} from the {organization.Name} {info.PlatformType}.",
                Properties = info,
            },
            actor, organization);

        _analyticsClient.Track("Slack App Uninstalled", AnalyticsFeature.Activations, actor, organization,
            new {
                app_id = info.AppId,
            });
    }

    public Task LogIntegrationStatusChangedAsync(Integration integration, Member actor) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync(
            $"{(integration.Enabled ? "Enabled" : "Disabled")} {integration.Type.Humanize()} integration.",
            actor.User, integration.Organization);
#pragma warning restore CS0618

    public async Task LogFormEventAsync(Member actor, Form form, string action, string description)
    {
        var auditEvent = new FormAuditEvent()
        {
            EntityId = form.Id,
            Properties = new FormAuditEventProperties(form.Key),
            Description = description,
        };
        await SaveAuditEventAsync(auditEvent, actor, form.Organization);
        _analyticsClient.Track(
            action,
            AnalyticsFeature.CustomForms,
            actor,
            form.Organization,
            new()
            {
                { "form", form.Key },
                { "staff", actor.IsStaff() },
            });
    }

    public async Task LogHubEventAsync(Member actor, Hub hub, string action, string description, HubAuditEventProperties? properties = null)
    {
        properties ??= new();
        var auditEvent = new HubAuditEvent()
        {
            EntityId = hub.Id,
            Properties = properties,
            Description = description,
            Room = hub.Room.Name,
            RoomId = hub.Room.PlatformRoomId,
        };
        await SaveAuditEventAsync(auditEvent, actor, hub.Organization);
        _analyticsClient.Track(
            action,
            AnalyticsFeature.Hubs,
            actor,
            hub.Organization,
            properties.ToAnalyticsProperties(actor));
    }

    public async Task LogAdminActivityAsync(string description, User actor, Organization organization)
    {
#pragma warning disable CS0618
        await SaveAdminAuditEventAsync(description, actor, organization);
#pragma warning restore CS0618
    }

    public Task LogDenyUser(Member subject, User actor) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync($"Denied access to {FormatUser(subject.User)}", actor, subject.Organization);
#pragma warning restore CS0618

    public Task LogArchiveUserAsync(Member subject, User actor) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync($"Archived user {FormatUser(subject.User)}", actor, subject.Organization);
#pragma warning restore CS0618

    public Task LogRestoreUserAsync(Member subject, User actor) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync($"Restored user {FormatUser(subject.User)}", actor, subject.Organization);
#pragma warning restore CS0618

    public Task LogAutoApproveChanged(User actor, Organization organization)
    {
        var verb = organization.AutoApproveUsers ? "Enabled" : "Disabled";
#pragma warning disable CS0618
        return SaveAdminAuditEventAsync($"{verb} the auto approve users setting.", actor, organization);
#pragma warning restore CS0618
    }

    public Task LogApiEnabledChanged(User actor, Organization organization)
    {
        var verb = organization.ApiEnabled ? "Enabled" : "Disabled";
#pragma warning disable CS0618
        return SaveAdminAuditEventAsync($"{verb} the ability to call the Abbot API for this organization.", actor, organization);
#pragma warning restore CS0618
    }

    public Task LogDefaultChatResponderEnabledChanged(User actor, Organization organization)
    {
        var verb = organization.FallbackResponderEnabled ? "Enabled" : "Disabled";
#pragma warning disable CS0618
        return SaveAdminAuditEventAsync($"{verb} the fallback chat responder for this organization.", actor, organization);
#pragma warning restore CS0618
    }

    public Task LogOrganizationShortcutChanged(User actor, char oldShortcut, Organization organization) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync($"The shortcut character changed from {oldShortcut.ToMarkdownInlineCode()} to {organization.ShortcutCharacter.ToMarkdownInlineCode()}.", actor, organization);
#pragma warning restore CS0618

    public Task LogOrganizationAvatarChanged(User actor, string avatarType, Organization organization) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync($"Updated the {avatarType} avatar", actor, organization);
#pragma warning restore CS0618

    public async Task LogManagedConversationsEnabledAsync(Member actor, Room room, Organization organization)
    {
        _analyticsClient.Track(
            "Conversation Management Enabled",
            AnalyticsFeature.Conversations,
            actor,
            room.Organization,
            new {
                room = room.PlatformRoomId,
            });

#pragma warning disable CS0618
        await SaveAdminAuditEventAsync(
            $"Enabled conversation tracking for {room.Name} (`{room.PlatformRoomId}`).",
            actor.User,
            organization);
#pragma warning restore CS0618
    }

    public async Task LogManagedConversationsDisabledAsync(Member actor, Room room, Organization organization)
    {
        _analyticsClient.Track(
            "Conversation Management Disabled",
            AnalyticsFeature.Conversations,
            actor,
            room.Organization,
            new {
                room = room.PlatformRoomId,
            });

#pragma warning disable CS0618
        await SaveAdminAuditEventAsync(
            $"Disabled conversation tracking for {room.Name} (`{room.PlatformRoomId}`).",
            actor.User,
            organization);
#pragma warning restore CS0618
    }

    Task LogEndpointChanged(User actor, Organization organization, string type, Uri? oldEndpoint, Uri? newEndpoint) =>
#pragma warning disable CS0618
        SaveAdminAuditEventAsync(
            $"The {type} endpoint changed from {oldEndpoint.ToMarkdownInlineCode()} to {newEndpoint.ToMarkdownInlineCode()}.",
            $"The {type} endpoint changed",
            actor, organization);
#pragma warning restore CS0618

    public async Task<AuditEvent> LogAuditEventAsync(AuditEventBuilder auditEventBuilder)
    {
        var auditEvent = new AuditEvent
        {
            Type = auditEventBuilder.Type,
            IsTopLevel = auditEventBuilder.IsTopLevel,
            Description = auditEventBuilder.Description,
            Details = auditEventBuilder.Details,
            StaffPerformed = auditEventBuilder.StaffPerformed,
            StaffOnly = auditEventBuilder.StaffOnly,
            StaffReason = auditEventBuilder.StaffReason,
            Properties = auditEventBuilder.Properties,
            EntityId = auditEventBuilder.EntityId,
            ActorMember = auditEventBuilder.Actor,
            Actor = auditEventBuilder.Actor.User,
            OrganizationId = auditEventBuilder.Organization.Id,
            ParentIdentifier = auditEventBuilder.ParentIdentifier,

            Created = _clock.UtcNow,
            TraceId = Activity.Current?.Id,
        };

        await _db.AuditEvents.AddAsync(auditEvent);
        await _db.SaveChangesAsync();

        return auditEvent;
    }

    public async Task<AuditEvent> LogAuditEventAsync(AuditEventBuilder auditEventBuilder, AnalyticsEventBuilder analyticsEventBuilder)
    {
        _analyticsClient.Track(
            analyticsEventBuilder.Event,
            analyticsEventBuilder.Feature,
            auditEventBuilder.Actor,
            auditEventBuilder.Organization,
            analyticsEventBuilder.Properties);

        return await LogAuditEventAsync(auditEventBuilder);
    }

    public async Task LogTrialStartedAsync(Member actor, Organization organization, TrialPlan trialPlan)
    {
        _analyticsClient.Track(
            "Trial started",
            AnalyticsFeature.Subscriptions,
            actor,
            organization,
            new()
            {
                { "trial_plan", trialPlan.Plan.ToString() },
                { "trial_expiration", trialPlan.Expiry }, // Rely on Segment to do the right thing here.
            });

        await LogAuditEventAsync(
            new()
            {
                Type = new("Trial", "Started"),
                Actor = actor,
                Organization = organization,
                Description = $"Trial of {trialPlan.Plan} plan started.",
                Properties = new {
                    TrialPlan = trialPlan.Plan,
                    TrialExpiration = trialPlan.Expiry,
                }
            });
    }

    public async Task LogPackagePublishedAsync(Package package, User actor, Organization organization)
    {
        await LogEntityCreatedAsync(package, actor, organization);
    }

    public async Task LogPackageUnlistedAsync(Package package, User actor, Organization organization)
    {
        await LogEntityDeletedAsync(package, actor, organization);
    }

    public async Task LogPackageChangedAsync(Package package, User actor, Organization organization)
    {
        var auditEvent = CreateAuditEventFromAuditableEntity(package, AuditOperation.Changed) as PackageEvent
                         ?? throw new InvalidOperationException($"Package.CreateAuditEventInstance did not return a {nameof(PackageEvent)}");
        auditEvent.Readme = package.Readme;
#pragma warning disable CS0618
        await SaveAuditEventAsync(auditEvent, actor, organization);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Log an event when a staff member has to view the code for a customer audit event.
    /// </summary>
    /// <param name="viewedAuditEvent">The audit event that the staff member viewed.</param>
    /// <param name="reason">The reason the staff member viewed the code for the event.</param>
    /// <param name="actor">The user that deleted the entity.</param>
    public Task LogStaffViewedCodeEventAsync(AuditEventBase viewedAuditEvent, string reason, User actor)
    {
        var staffEvent = new StaffViewedCodeAuditEvent
        {
            EntityId = viewedAuditEvent.Id,
            ViewedIdentifier = viewedAuditEvent.Identifier,
            Reason = reason,
            Description = $"(STAFF) Viewed code for log item {viewedAuditEvent.Identifier}"
        };
#pragma warning disable CS0618
        return SaveAuditEventAsync(staffEvent, actor, viewedAuditEvent.Organization);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Log an event when a staff member has to view a slack event for a customer.
    /// </summary>
    /// <param name="slackEvent">The slack event that the staff member viewed.</param>
    /// <param name="reason">The reason the staff member viewed the code for the event.</param>
    /// <param name="actor">The user that deleted the entity.</param>
    public async Task LogStaffViewedSlackEventAsync(SlackEvent slackEvent, string reason, User actor)
    {
        var staffEvent = new StaffViewedSlackEventContent
        {
            EntityId = slackEvent.Id,
            EventId = slackEvent.EventId,
            Reason = reason,
            Description = $"(STAFF) Viewed slack event content for SlackEvent {slackEvent.EventId}"
        };

        var seriousOrganization =
            await _db.Organizations.SingleOrDefaultAsync(o => o.PlatformId == WebConstants.ASeriousBizSlackId)
            ?? throw new InvalidOperationException($"The serious business organization {WebConstants.ASeriousBizSlackId} doesn't exist");
        var organization = await _db.Organizations.SingleOrDefaultAsync(o => o.PlatformId == slackEvent.TeamId)
                           ?? seriousOrganization;

#pragma warning disable CS0618
        await SaveAuditEventAsync(staffEvent, actor, organization);
#pragma warning restore CS0618

        if (organization.Id != seriousOrganization.Id)
        {
            // Add a copy of it to our serious business activity log as well
            var seriousStaffEvent = new StaffViewedSlackEventContent
            {
                EntityId = slackEvent.Id,
                EventId = slackEvent.EventId,
                Reason = reason,
                Description = $"(STAFF) Viewed slack event content for SlackEvent {slackEvent.EventId}"
            };
#pragma warning disable CS0618
            await SaveAuditEventAsync(seriousStaffEvent, actor, seriousOrganization);
#pragma warning restore CS0618
        }
    }

    public Task LogSkillNotFoundAsync(
        string command,
        string response,
        ResponseSource responseSource,
        User actor,
        Organization organization)
    {
        var auditEvent = new SkillNotFoundEvent
        {
            Command = command,
            Response = response,
            ResponseSource = responseSource,
            Description = $"Told Abbot {command.ToMarkdownInlineCode()} which did not match a skill. Abbot replied with "
                          + (responseSource is ResponseSource.SkillSearch
                              ? "suggested skills."
                              : "a default response.")
        };

#pragma warning disable CS0618
        return SaveAuditEventAsync(
#pragma warning restore CS0618
            auditEvent,
            actor,
            organization);
    }

    public async Task LogConversationLinkedAsync(Conversation conversation, ConversationLinkType linkType, string externalId,
        Member actor, Organization organization)
    {
        _analyticsClient.Track(
            "Conversation linked",
            AnalyticsFeature.Integrations,
            actor,
            organization,
            new()
            {
                { "room_type", conversation.Room.RoomType.ToString() },
                { "link_type", linkType.ToString() },
            });
        var conversationEvent = new ConversationLinkedEvent
        {
            EntityId = conversation.Id,
            LinkType = linkType,
            ExternalId = externalId,
            Description = $"Linked the conversation to a {linkType.ToDisplayString()}.",
        };
        await SaveAuditEventAsync(conversationEvent, actor, organization);
    }

    public async Task LogConversationAssignmentAsync(
        Conversation conversation,
        IReadOnlyList<Member> assignees,
        Member actor)
    {
        _analyticsClient.Track(
            "Conversation assignment",
            AnalyticsFeature.Integrations,
            actor,
            conversation.Organization,
            new()
            {
                { "room_type", conversation.Room.RoomType.ToString() },
            });

        var assignmentEvent = new AuditEvent
        {
            Type = new("Conversation", "AssignmentsChanged"),
            EntityId = conversation.Id,
            Description = assignees.Any()
                ? "assigned " + string.Join(", ", assignees.Select(a => a.DisplayName)) + " to this conversation."
                : "unassigned all members from this conversation.",
        };
        await SaveAuditEventAsync(assignmentEvent, actor, conversation.Organization);
    }

    public async Task LogConversationAttachedAsync(Conversation conversation, Hub hub, string hubThreadId, Member actor)
    {
        _analyticsClient.Track(
            "Conversation attached to Hub",
            AnalyticsFeature.Conversations,
            actor,
            conversation.Organization,
            new());

        var attachEvent = new AuditEvent
        {
            Type = new("Conversation", "AttachedToHub"),
            EntityId = conversation.Id,
            Description = $"attached this conversation to the '{hub.Name}' Hub.",
            Properties = new {
                HubId = hub.Id,
                HubThreadId = hubThreadId,
            }
        };

#pragma warning disable CS0618
        await SaveAuditEventAsync(attachEvent, actor.User, conversation.Organization);
#pragma warning restore CS0618
    }

    public async Task LogBotInvitedAsync(Member actor, Room room)
    {
        var organization = room.Organization;
        _analyticsClient.Track(
            "Bot Invited",
            AnalyticsFeature.Conversations,
            actor,
            organization,
            new {
                room = room.PlatformRoomId,
                botUserId = organization.PlatformBotUserId,
            });
#pragma warning disable CS0618
        await SaveAdminAuditEventAsync(
            $"Invited @{organization.BotName} to {room.Name} (`{room.PlatformRoomId}`).",
            actor.User,
            organization);
#pragma warning restore CS0618
    }

    public async Task LogRoomResponseTimesChangedAsync(
        Room room,
        TimeSpan? oldTarget,
        TimeSpan? oldDeadline,
        Member actor)
    {
        _analyticsClient.Track(
            "Room Response Time Changed",
            AnalyticsFeature.Conversations,
            actor,
            room.Organization,
            new()
            {
                { "room_type", room.RoomType.ToString() },
            });
        var auditEvent = new RoomResponseTimesChangedEvent
        {
            EntityId = room.Id,
            Room = room.Name,
            RoomId = room.PlatformRoomId,
            Properties = new ResponseTimeInfo(
                oldTarget,
                oldDeadline,
                room.TimeToRespond.Warning,
                room.TimeToRespond.Deadline,
                room.Organization.DefaultTimeToRespond.Warning,
                room.Organization.DefaultTimeToRespond.Deadline),
            Description = room.TimeToRespond.Deadline is null && room.TimeToRespond.Warning is null
                ? $"Removed response time settings for room `#{room.Name}`."
                  + (room.Organization.DefaultTimeToRespond.Warning is not null || room.Organization.DefaultTimeToRespond.Deadline is not null
                      ? " The organization default response time settings are now in effect."
                      : string.Empty)
                : $"Updated the response times for room `#{room.Name}`.",
        };
        await SaveAuditEventAsync(auditEvent, actor, room.Organization);
    }

    public async Task LogRoomRespondersChangedAsync(
        Room room,
        RoomRole roomRole,
        IReadOnlyList<Member> addedResponders,
        IReadOnlyList<Member> removedResponders,
        int respondersCount,
        Member actor)
    {
        if (roomRole is not RoomRole.EscalationResponder and not RoomRole.FirstResponder)
        {
            throw new UnreachableException($"Unexpected room role {roomRole}");
        }

        var responderType = roomRole switch
        {
            RoomRole.FirstResponder => "first",
            RoomRole.EscalationResponder => "escalation",
            _ => throw new UnreachableException()
        };

        var auditEvent = new RoomRespondersChangedEvent
        {
            EntityId = room.Id,
            Room = room.Name,
            RoomId = room.PlatformRoomId,
            Properties = new RespondersInfo(
                roomRole,
                addedResponders.Select(ResponderInfo.FromMember).ToList(),
                removedResponders.Select(ResponderInfo.FromMember).ToList(),
                respondersCount),
            Description = $"{responderType.Transform(To.SentenceCase)} responders updated for room `#{room.Name}`. The room now has {respondersCount.ToQuantity($"{responderType} responder")}.",
        };
        await SaveAuditEventAsync(auditEvent, actor, room.Organization);
        _analyticsClient.Track(
            "Room responders changed",
            AnalyticsFeature.Conversations,
            actor,
            room.Organization,
            new()
            {
                { "room_type", room.RoomType.ToString() },
                { "room_role", roomRole },
                { "added_responders", addedResponders.Count > 0 },
                { "removed_responders", removedResponders.Count > 0 },
                { "any_responders", respondersCount > 0 },
            });
    }

    public async Task LogRoomLinkedAsync(Room room, RoomLinkType linkType, string externalId, string displayName, Member actor, Organization organization)
    {
        _analyticsClient.Track(
            "Room linked",
            AnalyticsFeature.Integrations,
            actor,
            organization,
            new()
            {
                { "room_conversations_enabled", room.ManagedConversationsEnabled },
                { "room_type", room.RoomType.ToString() },
                { "link_type", linkType.ToString() },
            });
        var evt = new RoomLinkedEvent
        {
            EntityId = room.Id,
            LinkType = linkType,
            ExternalId = externalId,
            Description = $"Linked the room {room.Name} to a {linkType.Humanize()} \"{displayName}\".",
        };
        await SaveAuditEventAsync(evt, actor, organization);
    }

    public async Task LogRoomUnlinkedAsync(Room room, RoomLinkType linkType, string externalId, string displayName, Member actor,
        Organization organization)
    {
        _analyticsClient.Track(
            "Room unlinked",
            AnalyticsFeature.Integrations,
            actor,
            organization,
            new()
            {
                { "room_conversations_enabled", room.ManagedConversationsEnabled },
                { "room_type", room.RoomType.ToString() },
                { "link_type", linkType.ToString() },
            });
        var evt = new RoomUnlinkedEvent()
        {
            EntityId = room.Id,
            LinkType = linkType,
            ExternalId = externalId,
            Description = $"Unlinked the room {room.Name} from the {linkType.Humanize()} \"{displayName}\".",
        };
        await SaveAuditEventAsync(evt, actor, organization);
    }

    public async Task LogPurchaseSubscriptionAsync(Subscription subscription, Member actor, Organization organization)
    {
        _analyticsClient.Track(
            "Subscription Started",
            AnalyticsFeature.Subscriptions,
            actor,
            organization);

        await SaveBillingEventAsync(
            $"Purchased a {organization.PlanType} plan with {subscription.Items.Single().Quantity} agents.",
            subscription,
            actor.User,
            organization);
    }

    public async Task LogChangeSubscriptionAsync(
        PlanType previousPlan,
        Subscription subscription,
        User actor,
        Organization organization)
    {
        await SaveBillingEventAsync(
            $"Changed from the {previousPlan} plan to the {organization.PlanType} plan.",
            subscription,
            actor,
            organization);
    }

    public async Task LogUserPropertyChangedAsync(string property, string value, Member actor, Organization organization)
    {
        var suffix = value is { Length: > 0 }
            ? $" to `{value}`"
            : string.Empty;
        var auditEvent = new AuditEvent
        {
            Type = new("Member", "PropertyChanged"),
            EntityId = actor.Id,
            Description = $"Set their {property}{suffix} via the `my` skill."
        };
        await SaveAuditEventAsync(auditEvent, actor, organization);
    }

    public Task<BuiltInSkillRunEvent> LogBuiltInSkillRunAsync(ISkill skill, MessageContext messageContext)
    {
        return SaveBuiltInSkillRunAuditEventAsync(skill, messageContext, null);
    }

    public Task<BuiltInSkillRunEvent> LogBuiltInSkillRunAsync(ISkill skill, MessageContext messageContext, Exception exception)
    {
        return SaveBuiltInSkillRunAuditEventAsync(skill, messageContext, exception);
    }

    async Task<BuiltInSkillRunEvent> SaveBuiltInSkillRunAuditEventAsync(
        ISkill skill,
        MessageContext messageContext,
        Exception? exception)
    {
        var arguments = messageContext.Arguments;
        var displayRoom = messageContext.Room.ToAuditLogString();
        var command =
            arguments is { Count: 0 }
                ? $"{messageContext.SkillName.ToMarkdownInlineCode()} with no arguments"
                : $"{messageContext.SkillName} {arguments}".ToMarkdownInlineCode();

        var skillName = messageContext.SkillName;
        var description = $"Ran built-in skill {command} in {displayRoom}.";
        if (skill is ListSkill && skillName is not "list")
        {
            // Special case because the CustomListSkill can be called by an alias.
            // User called a custom list skill.
            var action = arguments is { Count: >= 2 }
                ? arguments[1].Value
                : "";

            var activity = action.ToUpperInvariant() switch
            {
                "ADD" => "Added an item to the",
                "REMOVE" => "Removed an item from the",
                "LIST" => "Listed all items in the",
                "INFO" => "Displayed info about the",
                _ => "Called the"
            };

            description = $"{activity} {arguments[0].Value.ToMarkdownInlineCode()} list in {displayRoom}";
        }

        var skillRunEvent = new BuiltInSkillRunEvent
        {
            SkillName = messageContext.SkillName,
            Description = description,
            Arguments = arguments.Value,
            ErrorMessage = exception?.Message,
            Room = messageContext.Room.Name,
            RoomId = messageContext.Room.PlatformRoomId
        };

        return await SaveAuditEventAsync(skillRunEvent, messageContext.FromMember, messageContext.Organization);
    }

    public async Task LogCancelSubscriptionAsync(PlanType previousPlan, Subscription subscription, User actor, Organization organization)
    {
        await SaveBillingEventAsync(
            $"Canceled {previousPlan} plan.",
            subscription,
            actor,
            organization);
    }

    Task<AuditEventBase> SaveBillingEventAsync(
        string description,
        Subscription subscription,
        User actor,
        Organization organization)
    {
        var auditEvent = new BillingEvent
        {
            PlanType = organization.PlanType,
            Description = description,
            CustomerId = subscription.CustomerId,
            BillingEmail = subscription.Customer?.Email,
            BillingName = subscription.Customer?.Name,
            SubscriptionId = subscription.Id
        };
#pragma warning disable CS0618
        return SaveAuditEventAsync<AuditEventBase>(auditEvent, actor, organization);
#pragma warning restore CS0618
    }

    [Obsolete("Use LogAuditEvent(AuditEventBuilder, ...) instead.")]
    Task<AdminAuditEvent> SaveAdminAuditEventAsync(string description, User actor, Organization organization) =>
        SaveAdminAuditEventAsync(description, null, actor, organization);

    [Obsolete("Use LogAuditEvent(AuditEventBuilder, ...) instead.")]
    Task<AdminAuditEvent> SaveAdminAuditEventAsync(string description, string? sanitizedDescription,
        User actor, Organization organization)
    {
        var auditEvent = new AdminAuditEvent
        {
            Description = sanitizedDescription ?? description,
            Details = description
        };
#pragma warning disable CS0618
        return SaveAuditEventAsync(auditEvent, actor, organization);
#pragma warning restore CS0618
    }

    async Task<TAuditEvent> SaveAuditEventAsync<TAuditEvent>(
        TAuditEvent auditEvent,
        Member actor,
        Organization organization) where TAuditEvent : AuditEventBase
    {
        auditEvent.ActorMember = actor;
#pragma warning disable CS0618
        return await SaveAuditEventAsync(auditEvent, actor.User, organization);
#pragma warning restore CS0618
    }

    [Obsolete("Use LogAuditEvent(AuditEventBuilder, ...) instead.")]
    protected virtual async Task<TAuditEvent> SaveAuditEventAsync<TAuditEvent>(
        TAuditEvent auditEvent,
        User actor,
        Organization organization) where TAuditEvent : AuditEventBase
    {
        auditEvent.Actor = actor;
        auditEvent.OrganizationId = organization.Id;
        auditEvent.Created = _clock.UtcNow;
        auditEvent.TraceId = Activity.Current?.Id;

        await _db.AuditEvents.AddAsync(auditEvent);
        await _db.SaveChangesAsync();

        return auditEvent;
    }

    static string FormatUser(User user)
    {
        return $"{user.DisplayName} (Id: {user.PlatformUserId})".ToMarkdownInlineCode();
    }
}

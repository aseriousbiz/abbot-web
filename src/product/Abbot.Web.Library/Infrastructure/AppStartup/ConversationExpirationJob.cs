using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Hangfire;
using Humanizer;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Signals;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Queries for expiring conversations and enqueues notifications, changes conversation state, and raises the
/// appropriate signals.
/// </summary>
[DisableConcurrentExecution(30)]
public class ConversationExpirationJob : IRecurringJob
{
    static readonly ILogger<ConversationExpirationJob> Log =
        ApplicationLoggerFactory.CreateLogger<ConversationExpirationJob>();

    readonly IConversationRepository _conversationRepository;
    readonly NotificationRepository _notificationRepository;
    readonly IUserRepository _userRepository;
    readonly ISystemSignaler _systemSignaler;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    readonly Histogram<long> _conversationInWarningCount;
    readonly Histogram<long> _conversationOverdueCount;
    readonly Histogram<long> _conversationWarningQueryDuration;
    readonly Histogram<long> _conversationOverdueQueryDuration;
    readonly Histogram<long> _conversationWarningGroupByDuration;
    readonly Histogram<long> _conversationOverdueGroupByDuration;
    readonly Histogram<long> _conversationEnqueueWarningDuration;
    readonly Histogram<long> _conversationEnqueueOverdueDuration;

    public ConversationExpirationJob(
        IConversationRepository conversationRepository,
        NotificationRepository notificationRepository,
        IUserRepository userRepository,
        ISystemSignaler systemSignaler,
        PlaybookDispatcher playbookDispatcher,
        IAnalyticsClient analyticsClient,
        IClock clock)
    {
        _conversationRepository = conversationRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _systemSignaler = systemSignaler;
        _playbookDispatcher = playbookDispatcher;
        _analyticsClient = analyticsClient;
        _clock = clock;

        _conversationInWarningCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-in-warning.count",
            "conversations-in-warning-period",
            "The number of conversations in the warning period.");
        _conversationOverdueCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-overdue.count",
            "conversations-overdue",
            "The number of overdue conversations.");
        _conversationWarningQueryDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-warnings-query.duration",
            "milliseconds",
            "The duration of the query to find conversations in the warning period.");
        _conversationOverdueQueryDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-overdue-query.duration",
            "milliseconds",
            "The duration of the query to find overdue conversations.");
        _conversationEnqueueWarningDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-enqueue-warnings.duration",
            "milliseconds",
            "The duration of the code to enqueue warning notifications.");
        _conversationWarningGroupByDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-warning-group-by.duration",
            "milliseconds",
            "The duration of the warning conversations group by operation.");
        _conversationEnqueueOverdueDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-enqueue-overdue.duration",
            "milliseconds",
            "The duration of the code to enqueue overdue notifications.");
        _conversationOverdueGroupByDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "expiration-job.conversations-overdue-group-by.duration",
            "milliseconds",
            "The duration of the deadline conversations group by operation.");
    }

    public static string Name => "Conversation Expiration Job";

    /// <summary>
    /// Queries the database for any conversations that are within the warning period and takes appropriate action.
    /// </summary>
    [Queue(HangfireQueueNames.NormalPriority)]
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await EnqueueNotificationsForOverdueConversationsAsync(_clock.UtcNow, cancellationToken);
    }

    public async Task EnqueueNotificationsForOverdueConversationsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        Log.TraceMethodEntered(typeof(ConversationExpirationJob), nameof(EnqueueNotificationsForOverdueConversationsAsync), utcNow);

        // Get all conversations that are within the warning period
        var conversationInWarningPeriod = await _conversationWarningQueryDuration.Time(() =>
            _conversationRepository.GetConversationsInWarningPeriodForTimeToRespond(
                utcNow,
                cancellationToken));
        _conversationInWarningCount.Record(conversationInWarningPeriod.Count);

        var warningConversations = _conversationWarningGroupByDuration.Time(() =>
            conversationInWarningPeriod
                .GroupBy(c => c.Organization, new FuncEqualityComparer<Organization>((x, y) => x.Id == y.Id))
                .ToList());

        foreach (var organizationWarnings in warningConversations)
        {
            var organization = organizationWarnings.Key;
            using var orgScope = Log.BeginOrganizationScope(organization);

            var orgTags = AbbotTelemetry.CreateOrganizationTags(organization);
            using var enqueuingWarningDuration = _conversationEnqueueWarningDuration.Time(orgTags);

            var defaultFirstResponders = await _userRepository.GetDefaultFirstRespondersAsync(
                organization,
                cancellationToken);

            foreach (var conversation in organizationWarnings)
            {
                using var convoScopes = Log.BeginConversationRoomAndHubScopes(conversation);

                if (cancellationToken.IsCancellationRequested)
                {
                    // This should be a safe place to cancel. The remaining conversations will be updated
                    // the next time this job runs.
                    throw new OperationCanceledException();
                }
                await UpdateOverdueAndEnqueueFirstRespondersAndAssigneesNotificationAsync(
                    utcNow,
                    conversation,
                    NotificationType.Warning,
                    defaultFirstResponders,
                    // We don't escalate for warnings.
                    Array.Empty<Member>());
            }
        }

        var overdueConversationsList = await _conversationOverdueQueryDuration.Time(() =>
            _conversationRepository.GetOverdueConversationsToNotifyAsync(
                utcNow,
                cancellationToken));
        _conversationOverdueCount.Record(overdueConversationsList.Count);

        var overdueConversations = _conversationOverdueGroupByDuration.Time(() =>
            overdueConversationsList
                .GroupBy(c => c.Organization, new FuncEqualityComparer<Organization>((x, y) => x.Id == y.Id))
                .ToList());

        foreach (var overdueGroup in overdueConversations)
        {
            var organization = overdueGroup.Key;
            using var orgScope = Log.BeginOrganizationScope(organization);

            var orgTags = AbbotTelemetry.CreateOrganizationTags(organization);
            using var enqueuingOverdueDuration = _conversationEnqueueOverdueDuration.Time(orgTags);

            var defaultFirstResponders = await _userRepository.GetDefaultFirstRespondersAsync(
                organization,
                cancellationToken);
            var defaultEscalationResponders = await _userRepository.GetDefaultEscalationRespondersAsync(
                organization,
                cancellationToken);

            foreach (var conversation in overdueGroup)
            {
                using var convoScopes = Log.BeginConversationRoomAndHubScopes(conversation);
                if (cancellationToken.IsCancellationRequested)
                {
                    // This should be a safe place to cancel. The remaining conversations will be updated
                    // the next time this job runs.
                    throw new OperationCanceledException();
                }

                await UpdateOverdueAndEnqueueFirstRespondersAndAssigneesNotificationAsync(
                    utcNow,
                    conversation,
                    NotificationType.Deadline,
                    defaultFirstResponders,
                    defaultEscalationResponders);
            }
        }
    }

    async Task UpdateOverdueAndEnqueueFirstRespondersAndAssigneesNotificationAsync(
        DateTime utcNow,
        Conversation conversation,
        NotificationType notificationType,
        IReadOnlyList<Member> defaultFirstResponders,
        IReadOnlyList<Member> defaultEscalationResponders)
    {
        var firstResponders = GetFirstResponders(conversation) is { Count: > 0 } respondersForRoom
            ? respondersForRoom
            : defaultFirstResponders;

        var escalationResponders = GetEscalationResponders(conversation) is { Count: > 0 } escalatorsForRoom
            ? escalatorsForRoom
            : defaultEscalationResponders;

        await NotifyRespondersAndUpdateOverdueStateAsync(
            utcNow,
            conversation,
            notificationType,
            conversation.Assignees,
            firstResponders,
            escalationResponders);
    }

    async Task NotifyRespondersAndUpdateOverdueStateAsync(
        DateTime utcNow,
        Conversation conversation,
        NotificationType notificationType,
        IReadOnlyCollection<Member> assignees,
        IReadOnlyCollection<Member> firstResponders,
        IReadOnlyCollection<Member> escalationResponders)
    {
        var organization = conversation.Organization;
        var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

        async Task UpdateWarningAsync(Conversation warningConversation)
        {
            await _conversationRepository.UpdateTimeToRespondWarningNotificationSentAsync(warningConversation, utcNow);
        }

        async Task UpdateOverdueAsync(Conversation overdueConversation)
        {
            await _conversationRepository.UpdateOverdueConversationAsync(overdueConversation, utcNow, abbot);
        }

        var deadline = (conversation.Room.TimeToRespond.Deadline
                        ?? conversation.Organization.DefaultTimeToRespond.Deadline)
            .GetValueOrDefault();

        var expiresAt = conversation.LastStateChangeOn + deadline;
        var expiresIn = _clock.UtcNow - expiresAt;

        // We only send the notification if:
        var shouldNotify =
            //  * Conversation Tracking is available in the org
            conversation.Organization.HasPlanFeature(PlanFeature.ConversationTracking)

            //  * Conversation Tracking is enabled for the room
            && conversation.Room.ManagedConversationsEnabled;

        if (shouldNotify)
        {
            // If the org setting is to notify on New conversations only, then we may need to suppress the notification.
            var notificationShouldBeSuppressed = NotificationShouldBeSuppressed(conversation, organization);

            // Construct a representation of the notification for the timeline event.
            var notificationInfo = CreateTimelineNotification(conversation, notificationType, expiresIn);

            // Send the notification to the escalation responders, if this is a deadline notification
            if (notificationType is NotificationType.Deadline && escalationResponders.Any())
            {
                notificationInfo.MentionGroups.Add(new MentionGroup(
                    NotificationRecipientType.EscalationResponder,
                    escalationResponders.Select(r => r.User.PlatformUserId).ToList()));

                if (!notificationShouldBeSuppressed)
                {
                    await _notificationRepository.EnqueueNotifications(conversation, escalationResponders);
                }
            }

            // Send to assignees, if any, and if no assignees then send to the first responders
            if (assignees.Any())
            {
                // Exclude anyone we've already sent to as an escalation responder
                var recipients = notificationType is NotificationType.Deadline
                    ? assignees.Except(escalationResponders).ToList()
                    : assignees;

                if (recipients.Any())
                {
                    notificationInfo.MentionGroups.Add(new MentionGroup(
                        NotificationRecipientType.Assignee,
                        recipients.Select(r => r.User.PlatformUserId).ToList()));

                    if (!notificationShouldBeSuppressed)
                    {
                        await _notificationRepository.EnqueueNotifications(conversation, recipients);
                    }
                }
            }
            else if (firstResponders.Any())
            {
                // Exclude anyone we've already sent to as an escalation responder
                var recipients = notificationType is NotificationType.Deadline
                    ? firstResponders.Except(escalationResponders).ToList()
                    : firstResponders;

                if (recipients.Any())
                {
                    notificationInfo.MentionGroups.Add(new MentionGroup(
                        NotificationRecipientType.FirstResponder,
                        recipients.Select(r => r.User.PlatformUserId).ToList()));

                    if (!notificationShouldBeSuppressed)
                    {
                        await _notificationRepository.EnqueueNotifications(conversation, recipients);
                    }
                }
            }

            await _conversationRepository.AddTimelineEventAsync(
                conversation,
                await _userRepository.EnsureAbbotMemberAsync(conversation.Organization),
                _clock.UtcNow,
                new NotificationEvent(notificationInfo, notificationShouldBeSuppressed));

            // Now update status.
            // This does mean that if the notification fails to send, we'll still update the conversation state.
            // But that's probably fine.
            RaiseOverdueSystemSignal(conversation, notificationType, abbot);
            await DispatchOverdueConversationTriggerAsync(conversation, notificationType);
            await TrackNotification(notificationType, assignees, firstResponders, escalationResponders, organization);
        }

        // We still update the state of the conversation even if we didn't send a notification.
        // We don't want a storm of notifications when an Organization turns tracking back on.
        // If a notification occurs while tracking is disabled, it's just lost to time.
        await (notificationType switch
        {
            NotificationType.Warning => UpdateWarningAsync(conversation),
            NotificationType.Deadline => UpdateOverdueAsync(conversation),
            _ => throw new InvalidOperationException($"Received an unexpected notification type {notificationType}!")
        });
    }

    static ConversationNotification CreateTimelineNotification(Conversation conversation, NotificationType notificationType,
        TimeSpan expiresIn)
    {
        var messageUrl = conversation.GetMessageUrl(conversation.Properties.LastSupporteeMessageId);

        var notification = notificationType switch
        {
            NotificationType.Deadline => new ConversationNotification()
            {
                Headline = "Expired",
                Message =
                    $"<{messageUrl}|This conversation> in {conversation.Room.ToMention()} has expired. Please reply as soon as possible.",
                Icon = "🚨",
            },
            NotificationType.Warning => new ConversationNotification()
            {
                Headline = $"Deadline in {expiresIn.Humanize()}",
                Message =
                    $"<{messageUrl}|This conversation> in {conversation.Room.ToMention()} will expire in {expiresIn.Humanize()}. Please reply as soon as possible.",
                Icon = "⚠️",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(notificationType), notificationType, null),
        };
        return notification;
    }

    // If NotifyOnNewConversationsOnly is set to true, we don't want to send notifications for conversations that
    // are still in the new state.
    static bool NotificationShouldBeSuppressed(Conversation conversation, Organization organization)
    {
        return organization is { Settings.NotifyOnNewConversationsOnly: true }
               && conversation.State is not ConversationState.New;
    }

    async Task TrackNotification(
        NotificationType notificationType,
        IReadOnlyCollection<Member> assignees,
        IReadOnlyCollection<Member> firstResponders,
        IReadOnlyCollection<Member> escalationResponders,
        Organization organization)
    {
        var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

        // This gives us some interesting information. For example, if notification type is "warning" and
        // first_responders and assignees is 0, then we know that the customer missed a warning notification.
        var missedNotification = assignees.Count is 0 && firstResponders.Count is 0
                                                      && (notificationType is NotificationType.Warning
                                                          || (notificationType is NotificationType.Deadline
                                                              && escalationResponders.Count is 0));

        _analyticsClient.Track(
            "Notification Sent",
            AnalyticsFeature.Conversations,
            abbot,
            organization,
            new {
                notification = notificationType.ToString(),
                first_responders = firstResponders.Count,
                escalation_responders = escalationResponders.Count,
                missed = missedNotification
            });
    }

    void RaiseOverdueSystemSignal(Conversation conversation, NotificationType notificationType, Member abbot)
    {
        var signal = SystemSignal.ConversationOverdueSignal;
        _systemSignaler.EnqueueSystemSignal(
            signal,
            arguments: $"{notificationType}",
            conversation.Organization,
            conversation.Room.ToPlatformRoom(),
            abbot,
            triggeringMessage: null);
    }

    async Task DispatchOverdueConversationTriggerAsync(Conversation conversation, NotificationType notificationType)
    {
        var outputs = new OutputsBuilder()
            .SetConversation(conversation)
            .Outputs;

        outputs["notification_type"] = Enum.GetName(notificationType);

        await _playbookDispatcher.DispatchAsync(
            ConversationOverdueTrigger.Id,
            outputs,
            conversation.Organization,
            PlaybookRunRelatedEntities.From(conversation));
    }

    static IReadOnlyList<Member> GetFirstResponders(Conversation conversation)
    {
        return conversation.Room.GetFirstResponders().ToList();
    }

    static IReadOnlyList<Member> GetEscalationResponders(Conversation conversation)
    {
        return conversation.Room.GetEscalationResponders().ToList();
    }
}

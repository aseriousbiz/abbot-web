using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Hangfire;
using Humanizer;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Job to query for expiring conversations and send a DM notification
/// to the first responder.
/// </summary>
[DisableConcurrentExecution(30)]
public class RespondersNotificationJob : IRecurringJob
{
    static readonly ILogger<RespondersNotificationJob> Log =
        ApplicationLoggerFactory.CreateLogger<RespondersNotificationJob>();

    readonly NotificationRepository _notificationRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly IUrlGenerator _urlGenerator;
    readonly IClock _clock;

    readonly Histogram<long> _pendingNotificationCount;
    readonly Histogram<long> _membersToNotifyCount;
    readonly Histogram<long> _groupByDuration;
    readonly Histogram<long> _queryDuration;
    readonly Histogram<long> _notificationsSendingDuration;
    readonly Histogram<long> _markNotificationsSentFailureCount;
    readonly Counter<int> _notificationsSentCount;
    readonly Counter<int> _notificationsFailedCount;

    public RespondersNotificationJob(
        NotificationRepository notificationRepository,
        ISlackApiClient slackApiClient,
        IUrlGenerator urlGenerator,
        IClock clock)
    {
        _notificationRepository = notificationRepository;
        _slackApiClient = slackApiClient;
        _urlGenerator = urlGenerator;
        _clock = clock;

        _pendingNotificationCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "notifications.pending.count",
            "pending-notifications",
            "The number of pending notifications to send.");
        _membersToNotifyCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "notifications.members.count",
            "members-to-notify",
            "The number of members to notify.");
        _notificationsSentCount = AbbotTelemetry.Meter.CreateCounter<int>(
            "notifications.sent.count",
            "notifications-sent",
            "The number of notification DMs sent successfully.");
        _notificationsFailedCount = AbbotTelemetry.Meter.CreateCounter<int>(
            "notifications.failed.count",
            "notifications-failed",
            "The number of notification DMs that failed to send.");
        _markNotificationsSentFailureCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "notifications.mark-sent-failure.count",
            "failures",
            "The number of failures trying to mark pending notifications as sent.");
        _groupByDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "notifications.group-by.duration",
            "milliseconds",
            "The duration of the group by operation.");
        _queryDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "notifications.query.duration",
            "milliseconds",
            "The duration of the query operation.");
        _notificationsSendingDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "notifications.sending.duration",
            "milliseconds",
            "The duration of sending notifications.");
    }

    public static string Name => "Abbot Responders Notifications";

    /// <summary>
    /// Queries the database for any conversations that are within the warning period and sends a DM to the first responder.
    /// </summary>
    [Queue(HangfireQueueNames.NormalPriority)]
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await NotifyRespondersAsync(_clock.UtcNow, cancellationToken);
    }

    public async Task NotifyRespondersAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        Log.TraceMethodEntered(typeof(RespondersNotificationJob), nameof(NotifyRespondersAsync), utcNow);

        var pendingNotifications = await _queryDuration.Time(
            () => _notificationRepository.GetPendingNotificationsAsync(cancellationToken));
        if (!pendingNotifications.Any())
        {
            return;
        }

        _pendingNotificationCount.Record(pendingNotifications.Count);

        var memberNotifications = _groupByDuration.Time(() => pendingNotifications.GroupBy(
            p => p.Member,
            new FuncEqualityComparer<Member>((x, y) => x.Id == y.Id))
            .ToList());
        _membersToNotifyCount.Record(memberNotifications.Count);

        Log.PendingNotificationsFound(pendingNotifications.Count, memberNotifications.Count);

        using var _ = _notificationsSendingDuration.Time();
        foreach (var memberGroup in memberNotifications)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            // Is it within the user's working hours? If the user has no working hours set, then we send anyways.
            var member = memberGroup.Key;
            if (!member.Properties.Notifications.OnExpiration)
            {
                Log.NotSubscribedToNotifications(member.User.PlatformUserId);
                continue;
            }

            if (member.IsInWorkingHours(utcNow))
            {
                await SendNotificationsAsync(member, memberGroup.ToReadOnlyList(), cancellationToken);
            }
            else
            {
                Log.OutsideWorkingHours(member.WorkingHours, member.User.PlatformUserId);
            }
        }
    }

    async Task SendNotificationsAsync(
        Member member,
        IReadOnlyList<PendingMemberNotification> pendingNotifications,
        CancellationToken cancellationToken)
    {
        var metricTags = AbbotTelemetry.CreateOrganizationTags(member.Organization);

        // Build up a notification.
        var conversations = pendingNotifications
            .Select(n => n.Conversation)
            .DistinctBy(c => c.Id)
            .ToList();

        var overdue = conversations
            .Where(c => c.State == ConversationState.Overdue)
            .ToList();

        var warnings = conversations
            .Where(c => c.State is ConversationState.New or ConversationState.NeedsResponse)
            .Where(ConversationRepository.WarningPeriodExpression(_clock.UtcNow).Compile())
            .ToList();

        Expect.NotNull(member.User);

        var conversationsCount = overdue.Count + warnings.Count;
        if (conversationsCount is 0)
        {
            Log.NoConversationsToNotify(member.User.PlatformUserId);
            foreach (var skipped in conversations)
            {
                Log.SkippingNotification(member.User.PlatformUserId, skipped, skipped.State, skipped.LastStateChangeOn, skipped.TimeToRespondWarningNotificationSent);
            }

            // Assume all pending notifications have already been handled
            var failureCount = await _notificationRepository.MarkPendingNotificationsSentAsync(
                member,
                pendingNotifications);
            _markNotificationsSentFailureCount.Record(failureCount, metricTags);
            return;
        }

        var blocks = CreateNotificationMessage(overdue, warnings).ToArray();

        if (cancellationToken.IsCancellationRequested)
        {
            // If cancellation is requested, we should stop and let the next iteration handle notifications.
            // Once we get past this point, we don't want to cancel the operation.
            // We're not going to increment cancellation request count here because we handle it in the outer loop.
            throw new OperationCanceledException();
        }

        // Send it.
        var response = await _slackApiClient.SendDirectMessageAsync(
            member.Organization,
            member.User,
            $"{(conversationsCount == 1 ? "A conversation needs" : "Some conversations need")} your attention.",
            blocks);

        if (response.Ok)
        {
            _notificationsSentCount.Add(1, metricTags);
            // Update the notification status.
            var failureCount = await _notificationRepository.MarkPendingNotificationsSentAsync(
                member,
                pendingNotifications);
            _markNotificationsSentFailureCount.Record(failureCount, metricTags);
        }
        else
        {
            _notificationsFailedCount.Add(1, metricTags);
        }
    }

    IEnumerable<ILayoutBlock> CreateNotificationMessage(
        IReadOnlyList<Conversation> overdue,
        IReadOnlyList<Conversation> warning)
    {
        if (overdue.Any())
        {
            yield return new Section(new MrkdwnText("🚨 The following conversations have not received a response within the *Deadline* response time."));
            var overdueList = overdue.Select(FormatOverdueConversation).ToMarkdownList();
            yield return new Section(new MrkdwnText(overdueList));
        }

        if (warning.Any())
        {
            yield return new Section(new MrkdwnText("⚠️ The following conversations have not received a response within the *Target* response time."));
            var warningList = warning.Select(FormatWarningConversation).ToMarkdownList();
            yield return new Section(new MrkdwnText(warningList));
        }
        yield return new Context($"You are receiving this message because you are an assignee, first responder, or escalation responder for these conversations. To change your notification settings, visit your <{_urlGenerator.AccountSettingsPage()}|account settings page>.");
    }

    static string FormatOverdueConversation(Conversation conversation)
    {
        var messageUrl = conversation.GetMessageUrl(conversation.Properties.LastSupporteeMessageId);
        return $"<{messageUrl}|Conversation> started by {conversation.StartedBy.ToMention()} in {conversation.Room.ToMention()}.";
    }

    string FormatWarningConversation(Conversation conversation)
    {
        var deadline = (conversation.Room.TimeToRespond.Deadline
                        ?? conversation.Organization.DefaultTimeToRespond.Deadline)
            .GetValueOrDefault();

        var expiresAt = conversation.LastStateChangeOn + deadline;
        var expiresIn = _clock.UtcNow - expiresAt;

        var messageUrl = conversation.GetMessageUrl(conversation.Properties.LastSupporteeMessageId);
        return $"<{messageUrl}|Conversation> started by {conversation.StartedBy.ToMention()} in {conversation.Room.ToMention()} will expire in {expiresIn.Humanize()}.";
    }
}

static partial class RespondersNotificationJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Found {PendingNotificationsCount} pending notifications for {MembersCount} members.")]
    public static partial void PendingNotificationsFound(
        this ILogger<RespondersNotificationJob> logger,
        int pendingNotificationsCount,
        int membersCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Pending Notifications found, but it's outside of member {UserId}'s {WorkingHours} working hours.")]
    public static partial void OutsideWorkingHours(
        this ILogger<RespondersNotificationJob> logger,
        WorkingHours? workingHours,
        string userId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "User {UserId} is not subscribed to Conversation notifications.")]
    public static partial void NotSubscribedToNotifications(
        this ILogger<RespondersNotificationJob> logger, string userId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "No conversations to notify about for {UserId}.")]
    public static partial void NoConversationsToNotify(
        this ILogger<RespondersNotificationJob> logger, string userId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Skipping conversation for {UserId}: {ConversationId} is {ConversationState}, last changed {LastStateChangeOn}, sent {TimeToRespondWarningNotificationSent}")]
    public static partial void SkippingNotification(
        this ILogger<RespondersNotificationJob> logger,
        string userId,
        Id<Conversation> conversationId,
        ConversationState conversationState,
        DateTime lastStateChangeOn,
        DateTime? timeToRespondWarningNotificationSent);
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Logging;

namespace Serious.Abbot.Repositories;

public class NotificationRepository
{
    static readonly ILogger<NotificationRepository> Log =
        ApplicationLoggerFactory.CreateLogger<NotificationRepository>();

    readonly AbbotContext _db;
    readonly IClock _clock;

    public NotificationRepository(AbbotContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task EnqueueNotifications(Conversation conversation, IEnumerable<Member> members)
    {
        Log.TraceMethodEntered(typeof(NotificationRepository), nameof(EnqueueNotifications), new { conversation.Id });

        var existingNotifications = await _db.PendingMemberNotifications
            .Include(n => n.Conversation.Organization)
            .Where(n => n.ConversationId == conversation.Id)
            .Where(n => n.DateSentUtc == null)
            .ToListAsync();

        var lookup = existingNotifications.Select(n => n.MemberId).Distinct().ToHashSet();

        foreach (var member in members
                     .Where(m => m.Properties.Notifications.OnExpiration)
                     .Where(m => !lookup.Contains(m.Id)))
        {
            var notification = new PendingMemberNotification
            {
                ConversationId = conversation.Id,
                MemberId = member.Id,
                Conversation = conversation,
                Member = member,
                Created = _clock.UtcNow,
                Organization = conversation.Organization,
                OrganizationId = conversation.OrganizationId,
                NotBeforeUtc = member.IsInWorkingHours(_clock.UtcNow)
                    ? null
                    : member.GetNextWorkingHoursStartDateUtc(_clock.UtcNow)
            };

            await _db.PendingMemberNotifications.AddAsync(notification);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                await Task.Delay(100);
                await _db.SaveChangesAsync();
            }
        }
    }

    public async Task<IReadOnlyList<PendingMemberNotification>> GetPendingNotificationsAsync(
        CancellationToken cancellationToken)
    {
        return await _db.PendingMemberNotifications
            .Include(n => n.Conversation.Organization)
            .Include(n => n.Conversation.StartedBy.User)
            .Include(n => n.Conversation.Room)
            .Include(n => n.Member.User)
            .Where(n => n.NotBeforeUtc == null || _clock.UtcNow > n.NotBeforeUtc)
            .Where(n => n.Conversation.Organization.Enabled)
            .Where(n => n.Conversation.Room.ManagedConversationsEnabled) // If a room no longer has managed conversations enabled, we shouldn't send notifications.
            .Where(n => n.Created > _clock.UtcNow.AddDays(-14)) // Don't include stranded enqueued notifications. Later, we need a cleanup job for these. A pending notification could be stranded if the room's managed conversations setting was disabled after the notification was enqueued.
            .Where(n => n.DateSentUtc == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> MarkPendingNotificationsSentAsync(Member member, IEnumerable<PendingMemberNotification> notifications)
    {
        Log.TraceMethodEntered(typeof(NotificationRepository), nameof(MarkPendingNotificationsSentAsync), null);

        foreach (var notification in notifications)
        {
            notification.DateSentUtc = _clock.UtcNow;
        }

        // Yes, this is cheesy. We'll do something better later.
        try
        {
            await _db.SaveChangesAsync();
            return 0;
        }
        catch (Exception)
        {
            await Task.Delay(100);
            try
            {
                await _db.SaveChangesAsync();
                return 1;
            }
            catch (Exception)
            {
                await Task.Delay(1000);
                try
                {
                    await _db.SaveChangesAsync();
                    return 2;
                }
                catch (Exception e)
                {
                    Log.ExceptionMarkingPendingNotificationsSent(e, member);
                    return 3;
                }
            }
        }
    }
}

static partial class NotificationRepositoryLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Exception setting pending notifications sent for {MemberId}")]
    public static partial void ExceptionMarkingPendingNotificationsSent(
        this ILogger<NotificationRepository> logger,
        Exception exception,
        Id<Member> memberId);
}

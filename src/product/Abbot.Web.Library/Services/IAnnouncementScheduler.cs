using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Services;

public interface IAnnouncementScheduler
{
    /// <summary>
    /// Schedules the announcement to be sent on its scheduled date (see <see cref="Announcement.ScheduledDateUtc"/>.
    /// </summary>
    /// <param name="announcement">The announcement to broadcast.</param>
    /// <param name="actor">The user scheduling the announcement.</param>
    /// <param name="scheduleReminder">If set to <c>true</c>, schedules a reminder message, not the broadcast. When the reminder is sent, the broadcast will be scheduled.</param>
    /// <returns><c>true</c> if the announcement is scheduled. Returns <c>false</c> if it's too late.</returns>
    Task<bool> ScheduleAnnouncementBroadcastAsync(Announcement announcement, User actor, bool scheduleReminder);

    /// <summary>
    /// Schedules all sent announcements to be sent with the new announcement text.
    /// </summary>
    /// <param name="announcement">The announcement to broadcast.</param>
    /// <param name="actor">The user scheduling the announcement.</param>
    /// <returns><c>true</c> if the announcement is scheduled. Returns <c>false</c> if it's too late.</returns>
    Task<bool> ScheduleAnnouncementUpdateAsync(Announcement announcement, User actor);

    /// <summary>
    /// Attempts to un-schedule the announcement. If the announcement already has started sending,
    /// it will not be unscheduled.
    /// </summary>
    /// <param name="announcement">The announcement to un-schedule.</param>
    /// <param name="actor">The person un-scheduling the announcement.</param>
    /// <returns></returns>
    Task<bool> UnscheduleAnnouncementBroadcastAsync(Announcement announcement, User actor);
}

using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Services;

public class AnnouncementScheduler : IAnnouncementScheduler
{
    static readonly ILogger<AnnouncementScheduler> Log = ApplicationLoggerFactory.CreateLogger<AnnouncementScheduler>();

    static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(1);
    static readonly TimeSpan ReminderThresholdPadding = TimeSpan.FromMinutes(10);
    readonly IAnnouncementsRepository _announcementsRepository;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly IClock _clock;

    public AnnouncementScheduler(
        IAnnouncementsRepository announcementsRepository,
        IBackgroundJobClient backgroundJobClient,
        IClock clock)
    {
        _announcementsRepository = announcementsRepository;
        _backgroundJobClient = backgroundJobClient;
        _clock = clock;
    }

    /// <summary>
    /// Schedules the announcement to be sent on its scheduled date (see <see cref="Announcement.ScheduledDateUtc"/>.
    /// </summary>
    /// <param name="announcement">The announcement to broadcast.</param>
    /// <param name="actor">The user scheduling the announcement.</param>
    /// <param name="scheduleReminder">If set to <c>true</c>, schedules a reminder message, not the broadcast. When the reminder is sent, the broadcast will be scheduled.</param>
    /// <returns><c>true</c> if the announcement is scheduled. Returns <c>false</c> if it's too late.</returns>
    public async Task<bool> ScheduleAnnouncementBroadcastAsync(
        Announcement announcement,
        User actor,
        bool scheduleReminder)
    {
        if (announcement.DateStartedUtc is not null)
        {
            Log.AnnouncementAlreadyStarted(announcement.Id, announcement.ScheduledDateUtc, announcement.DateStartedUtc);
            // The announcement already started.
            return false;
        }

        if (announcement.ScheduledJobId is not null)
        {
            // Attempt to delete the existing job. Under the hood, this acquires a distributed lock.
            if (!_backgroundJobClient.Delete(announcement.ScheduledJobId))
            {
                Log.JobRescheduleFailed(announcement.Id,
                    announcement.ScheduledJobId,
                    announcement.ScheduledDateUtc,
                    announcement.DateStartedUtc);
                // This means the job has already started, is completed, or has been deleted.
                return false;
            }

            Log.JobDeletedForReschedule(announcement.Id, announcement.ScheduledJobId);
        }

        // If the schedule date is more than an hour into the future, we schedule a reminder. The reminder will
        // schedule the announcement for the scheduled date.
        var scheduledDateUtc = announcement.ScheduledDateUtc;

        // If the scheduled date is further in the future than the reminder interval (plus padding), we
        // schedule a reminder message to occur prior to the scheduled date.
        //
        // For example, if the ReminderInterval is 1 hour, and the padding is 20 minutes, then we will schedule a
        // reminder message if the scheduled date is more than 1 hour and 20 minutes in the future.
        // The reminder message will be sent 1 hour before the broadcast.
        // The method that sends the reminder message is responsible for scheduling the broadcast. The padding we
        // use here ensures that when the reminder message is sent, we're well within the interval so that
        // calling the method to schedule the broadcast will schedule the broadcast and not the reminder.
        var reminderThreshold = _clock
            .UtcNow.
            Add(ReminderInterval)
            .Add(ReminderThresholdPadding);
        var jobId = scheduleReminder && scheduledDateUtc > reminderThreshold
            ? ScheduleReminder(announcement, scheduledDateUtc.Value)
            : ScheduleBroadcast(announcement, scheduledDateUtc ?? _clock.UtcNow);

        announcement.ScheduledJobId = jobId;
        await _announcementsRepository.UpdateAsync(announcement, actor);

        // TODO: Schedule a daily roll-up.

        return true;
    }

    public async Task<bool> ScheduleAnnouncementUpdateAsync(Announcement announcement, User actor)
    {
        if (announcement.DateStartedUtc is null || announcement.ScheduledJobId is not { Length: > 0 } scheduledJobId)
        {
            // The announcement hasn't started yet. Nothing to do
            return false;
        }

        await _announcementsRepository.UpdateAsync(announcement, actor);

        // Make sure this occurs after the job to send announcements is done to prevent issues.
        _backgroundJobClient.ContinueJobWith<AnnouncementSender>(scheduledJobId,
            sender => sender.UpdateSentAnnouncementMessagesAsync(announcement.Id));

        return true;
    }

    public async Task<bool> UnscheduleAnnouncementBroadcastAsync(Announcement announcement, User actor)
    {
        if (announcement.DateStartedUtc is not null || announcement.ScheduledDateUtc <= _clock.UtcNow)
        {
            Log.AnnouncementAlreadyStarted(announcement.Id, announcement.ScheduledDateUtc, announcement.DateStartedUtc);
            // The announcement already started.
            return false;
        }

        if (announcement.ScheduledJobId is not null)
        {
            // Attempt to delete the existing job. Under the hood, this acquires a distributed lock.
            if (!_backgroundJobClient.Delete(announcement.ScheduledJobId))
            {
                Log.JobRescheduleFailed(announcement.Id,
                    announcement.ScheduledJobId,
                    announcement.ScheduledDateUtc,
                    announcement.DateStartedUtc);
                // This means the job has already started, is completed, or was previously deleted.
                return false;
            }

            Log.JobDeletedForReschedule(announcement.Id, announcement.ScheduledJobId);
        }
        await _announcementsRepository.RemoveAsync(announcement, actor);
        return true;
    }

    string ScheduleReminder(Announcement announcement, DateTime scheduledDateUtc)
    {
        var reminderDateUtc = scheduledDateUtc.Subtract(ReminderInterval);
        var jobId = _backgroundJobClient.Schedule<AnnouncementSender>(
            s => s.SendReminderAsync(announcement.Id),
            reminderDateUtc);
        Log.ReminderScheduled(announcement.Id, jobId, reminderDateUtc);
        return jobId;
    }

    string ScheduleBroadcast(Announcement announcement, DateTime scheduledDateUtc)
    {
        var jobId = _backgroundJobClient.Schedule<AnnouncementSender>(
            s => s.BroadcastAnnouncementAsync(announcement.Id),
            scheduledDateUtc);
        Log.BroadcastScheduled(announcement.Id, jobId, scheduledDateUtc);
        return jobId;
    }
}

public static partial class AnnouncementSchedulerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Announcement ({AnnouncementId} scheduled to start at {ScheduledStartDate} already started at {StartDate}.")]
    public static partial void AnnouncementAlreadyStarted(
        this ILogger<AnnouncementScheduler> logger,
        int announcementId,
        DateTime? scheduledStartDate,
        DateTime? startDate);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Failed to reschedule job {JobId} for Announcement {AnnouncementId}. It may already be running. It was scheduled to start at {ScheduledStartDate} already started at {StartDate}.")]
    public static partial void JobRescheduleFailed(
        this ILogger<AnnouncementScheduler> logger,
        int announcementId,
        string jobId,
        DateTime? scheduledStartDate,
        DateTime? startDate);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Deleting job {JobId} for Announcement {AnnouncementId} so we can reschedule.")]
    public static partial void JobDeletedForReschedule(
        this ILogger<AnnouncementScheduler> logger,
        int announcementId,
        string jobId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Reminder for announcement {AnnouncementId} scheduled with Job Id {JobId} to be sent {ReminderScheduledDateUtc}.")]
    public static partial void ReminderScheduled(
        this ILogger<AnnouncementScheduler> logger,
        int announcementId,
        string jobId,
        DateTime reminderScheduledDateUtc);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Broadcast for announcement {AnnouncementId} scheduled with Job Id {JobId} to be sent {ScheduledDateUtc}.")]
    public static partial void BroadcastScheduled(
        this ILogger<AnnouncementScheduler> logger,
        int announcementId,
        string jobId,
        DateTime scheduledDateUtc);
}

using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Hangfire;
using Hangfire.States;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Xunit;

public class AnnouncementSchedulerTests
{
    public class TheScheduleAnnouncementBroadcastAsyncMethod
    {
        [Fact]
        public async Task OutsideOfReminderThresholdSchedulesBackgroundJobToSendAnnouncementOnScheduledDate()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var actor = env.TestData.User;
            var scheduledDate = env.Clock.UtcNow.AddMinutes(20);
            var announcement = new Announcement
            {
                Id = 42,
                ScheduledDateUtc = scheduledDate
            };
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.ScheduleAnnouncementBroadcastAsync(
                announcement,
                actor,
                scheduleReminder: true);

            Assert.True(result);
            var (job, state, id) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            var scheduledState = Assert.IsType<ScheduledState>(state);
            Assert.Equal(scheduledDate, scheduledState.EnqueueAt);
            Assert.Equal(nameof(AnnouncementSender.BroadcastAnnouncementAsync), job.Method.Name);
            var arg = Assert.Single(job.Args);
            Assert.Equal(42, arg);
            Assert.Equal(id, announcement.ScheduledJobId);
        }

        [Fact]
        public async Task InsideReminderThresholdSchedulesBackgroundJobToSendReminderAnHourBeforeScheduledDate()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var actor = env.TestData.User;
            var scheduledDate = env.Clock.UtcNow.AddDays(1);
            var announcement = new Announcement
            {
                Id = 42,
                ScheduledDateUtc = scheduledDate,
            };
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.ScheduleAnnouncementBroadcastAsync(
                announcement,
                actor,
                scheduleReminder: true);

            Assert.True(result);
            var (job, state, id) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            var scheduledState = Assert.IsType<ScheduledState>(state);
            Assert.Equal(scheduledDate.AddHours(-1), scheduledState.EnqueueAt);
            Assert.Equal(nameof(AnnouncementSender.SendReminderAsync), job.Method.Name);
            var arg = Assert.Single(job.Args);
            Assert.Equal(42, arg);
            Assert.Equal(id, announcement.ScheduledJobId);
        }

        [Fact]
        public async Task ReturnsFalseIfAlreadyScheduled()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.User;
            var announcement = new Announcement { DateStartedUtc = new DateTime() };
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.ScheduleAnnouncementBroadcastAsync(
                announcement,
                actor,
                scheduleReminder: true);

            Assert.False(result);
        }
    }

    public class TheUnscheduleAnnouncementBroadcastAsyncMethod
    {
        [Fact]
        public async Task ReturnsFalseIfAlreadyStarted()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.User;
            var announcement = new Announcement { DateStartedUtc = env.Clock.UtcNow };
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.UnscheduleAnnouncementBroadcastAsync(
                announcement,
                actor);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsFalseIfAlreadyScheduledToStart()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.User;
            var announcement = new Announcement { DateStartedUtc = null, ScheduledDateUtc = env.Clock.UtcNow };
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.UnscheduleAnnouncementBroadcastAsync(
                announcement,
                actor);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsFalseIfUnableToDeleteBackgroundJob()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBackgroundJobClient>(out var backgroundClient)
                .Build();
            backgroundClient.Delete("job-id").Returns(false);
            var actor = env.TestData.User;
            var announcement = new Announcement
            {
                DateStartedUtc = null,
                ScheduledDateUtc = env.Clock.UtcNow.AddDays(1),
                ScheduledJobId = "job-id"
            };
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.UnscheduleAnnouncementBroadcastAsync(
                announcement,
                actor);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsTrueIfAbleToDeleteBackgroundJob()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBackgroundJobClient>(out var backgroundClient)
                .Build();
            backgroundClient.Delete("job-id").Returns(true);
            var actor = env.TestData.User;
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "message-id",
                targetRooms: targetRoom);
            var scheduler = env.Activate<AnnouncementScheduler>();

            var result = await scheduler.UnscheduleAnnouncementBroadcastAsync(
                announcement,
                actor);

            Assert.True(result);
            Assert.True(announcement.IsDeleted);
        }
    }
}

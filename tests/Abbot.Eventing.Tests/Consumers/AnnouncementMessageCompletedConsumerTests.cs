using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Eventing.Messages;
using Serious.Slack.BlockKit;
using Serious.TestHelpers.CultureAware;

public class AnnouncementMessageCompletedConsumerTests
{
    public class TheConsumeMethod
    {
        [Fact]
        [UseCulture("en-US")]
        public async Task SendsDirectMessageToAnnouncementCreatorWhenAllMessagesAreSent()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AnnouncementMessageCompletedConsumer>()
                .Build();
            var from = env.TestData.User;
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync(roomType: RoomType.PrivateChannel);
            var announcement = new Announcement
            {
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                    new() { Room = room2 },
                },
                ScheduledDateUtc = env.Clock.UtcNow.AddHours(1)
            };
            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[1].SentDateUtc = env.Clock.UtcNow;
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var message = new AnnouncementMessageCompleted(announcement);

            await env.PublishAndWaitForConsumptionAsync(message);

            var channelsList = announcement.Messages.ToRoomMentionList();
            Assert.Equal($"<#{room1.PlatformRoomId}> and <#{room2.PlatformRoomId}>", channelsList);
            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal($":mega: :sparkles: I’ve posted your announcement in the following channels: <#{room1.PlatformRoomId}> and <https://{env.TestData.Organization.Domain}/archives/{room2.PlatformRoomId}|#{room2.Name}>.", postedMessage.Text);
            Assert.Equal(from.PlatformUserId, postedMessage.Channel);
        }

        [Fact]
        public async Task DoesNotSendsDirectMessageToAnnouncementCreatorIfAnnouncementDateCompletedAlreadySet()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AnnouncementMessageCompletedConsumer>()
                .Build();
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                    new() { Room = room2 },
                },
                ScheduledDateUtc = env.Clock.UtcNow.AddHours(1),
                DateCompletedUtc = env.Clock.UtcNow
            };

            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var message = new AnnouncementMessageCompleted(announcement);

            await env.PublishAndWaitForConsumptionAsync(message);

            Assert.Empty(env.SlackApi.PostedMessages);
        }

        [Fact]
        [UseCulture("en-US")]
        public async Task SendsDirectMessageWithFailuresToAnnouncementCreator()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AnnouncementMessageCompletedConsumer>()
                .Build();
            var from = env.TestData.User;
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                    new() { Room = room2 },
                },
                ScheduledDateUtc = env.Clock.UtcNow.AddHours(1)
            };
            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[1].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[1].ErrorMessage = "Failed to send message";
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var message = new AnnouncementMessageCompleted(announcement);

            await env.PublishAndWaitForConsumptionAsync(message);

            var channelsList = announcement.Messages.ToRoomMentionList();
            Assert.Equal($"<#{room1.PlatformRoomId}> and <#{room2.PlatformRoomId}>", channelsList);
            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal($":mega: :sparkles: I’ve posted your announcement in the following channels: <#{room1.PlatformRoomId}>.", postedMessage.Text);
            Assert.NotNull(postedMessage.Blocks);
            var section = Assert.IsType<Section>(postedMessage.Blocks[0]);
            Assert.NotNull(section.Text);
            Assert.EndsWith($"\n\n:warning: I’ve also failed to post in the following channels: <#{room2.PlatformRoomId}>.", section.Text.Text);
            Assert.Equal(from.PlatformUserId, postedMessage.Channel);
        }

        [Fact]
        [UseCulture("en-US")]
        public async Task SendsTotalFailureMessageWhenNoSuccesses()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AnnouncementMessageCompletedConsumer>()
                .Build();
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                    new() { Room = room2 },
                },
                ScheduledDateUtc = env.Clock.UtcNow.AddHours(1)
            };
            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[0].ErrorMessage = "Failed to send message";
            announcement.Messages[1].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[1].ErrorMessage = "Failed to send message";
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var message = new AnnouncementMessageCompleted(announcement);

            await env.PublishAndWaitForConsumptionAsync(message);

            var channelsList = announcement.Messages.ToRoomMentionList();
            Assert.Equal($"<#{room1.PlatformRoomId}> and <#{room2.PlatformRoomId}>", channelsList);
            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal($":warning: I’ve failed to post your announcement in any of the channels you specified: <#{room1.PlatformRoomId}> and <#{room2.PlatformRoomId}>.", postedMessage.Text);
        }
    }
}

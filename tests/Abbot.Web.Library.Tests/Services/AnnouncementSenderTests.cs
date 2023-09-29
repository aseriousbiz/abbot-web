using Abbot.Common.TestHelpers;
using MassTransit;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Services;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;
using Serious.TestHelpers;

public class AnnouncementSenderTests
{
    public class TheBroadcastAnnouncementAsyncMethod
    {
        [Fact]
        public async Task EnqueueSendingMessageToEachRoom()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                    new() { Room = room2 }
                }
            };
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationHistoryResponse(
                env.TestData.Organization.ApiToken!.Reveal(),
                sourceRoom.PlatformRoomId,
                new[]
                {
                    new SlackMessage
                    {
                        Timestamp = "1234567.32434",
                        Text = "The source message"
                    }
                });
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var sender = env.Activate<AnnouncementSender>();

            await sender.BroadcastAnnouncementAsync(announcement.Id);

            await env.ReloadAsync(announcement);
            Assert.Equal("The source message", announcement.Text);
            Assert.Equal(env.Clock.UtcNow, announcement.DateStartedUtc);

            await env.ReloadAsync(announcement.Messages.ToArray());

            Assert.Equal(
                new[] { room1.Id, room2.Id },
                announcement.Messages.Select(m => m.RoomId));

            foreach (var message in announcement.Messages)
            {
                env.BackgroundJobClient.DidEnqueue<AnnouncementSender>(
                    x => x.SendAnnouncementMessageAsync(message.Id, null));
            }
        }

        [Fact]
        public async Task EnqueueSendingAnnouncementWithoutMessagesToEachTrackedExternalRoomWithBot()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var rooms = new[]
            {
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, shared: true), // 0
                await env.CreateRoomAsync(managedConversationsEnabled: false, botIsMember: true, shared: true),
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: false, shared: true),
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, shared: false),
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, shared: true), // 4
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, shared: true, archived: true),
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, shared: true, deleted: true),
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, shared: true), // 7
            };
            var announcement = new Announcement
            {
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>(),
            };
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationHistoryResponse(
                env.TestData.Organization.ApiToken!.Reveal(),
                sourceRoom.PlatformRoomId,
                new[]
                {
                    new SlackMessage
                    {
                        Timestamp = "1234567.32434",
                        Text = "The source message"
                    }
                });
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var sender = env.Activate<AnnouncementSender>();

            await sender.BroadcastAnnouncementAsync(announcement.Id);

            await env.ReloadAsync(announcement);
            Assert.Equal("The source message", announcement.Text);
            Assert.Equal(env.Clock.UtcNow, announcement.DateStartedUtc);

            // Ensure new Messages were saved
            await env.ReloadAsync(announcement.Messages.ToArray());

            Assert.Equal(
                new[] { rooms[0].Id, rooms[4].Id, rooms[7].Id },
                announcement.Messages.Select(m => m.RoomId).OrderBy(id => id));

            foreach (var message in announcement.Messages)
            {
                env.BackgroundJobClient.DidEnqueue<AnnouncementSender>(
                    x => x.SendAnnouncementMessageAsync(message.Id, null));
            }
        }
    }

    public class TheSendAnnouncementMessageAsyncMethod
    {
        [Theory]
        [InlineData(false, null)]
        [InlineData(true, null)]
        [InlineData(true, "https://example.com/custom.png")]
        public async Task PostsAnnouncementInChannelAndEnqueuesCompletionMessage(bool sendAsBot, string? botResponseAvatar)
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IPublishEndpoint>(out var publishEndpoint)
                .Build();
            var org = env.TestData.Organization;
            org.BotResponseAvatar = botResponseAvatar;
            var from = env.TestData.User;
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                SendAsBot = sendAsBot,
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = org,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                }
            };
            from.Avatar = "https://example.com/avatar.png";
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var sender = env.Activate<AnnouncementSender>();

            await sender.SendAnnouncementMessageAsync(announcement.Messages[0].Id, null);

            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal("The announcement message", postedMessage.Text);
            Assert.Equal(room1.PlatformRoomId, postedMessage.Channel);
            Assert.Equal(sendAsBot ? org.BotResponseAvatar ?? org.BotAvatar : from.Avatar, postedMessage.IconUrl?.ToString());
            Assert.Equal(sendAsBot ? org.BotName : from.DisplayName, postedMessage.UserName);
            Assert.Equal(env.Clock.UtcNow, announcement.Messages[0].SentDateUtc);
            await publishEndpoint.Received().Publish(new AnnouncementMessageCompleted(announcement));
        }

        [Fact]
        public async Task ThrowsExceptionIfRetryAttemptsNotExhausted()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var from = env.TestData.User;
            env.Clock.Freeze();
            env.SlackApi.AddMessageInfoResponse(
                organization.ApiToken!.Reveal(),
                "The announcement message",
                new MessageResponse
                {
                    Ok = false,
                    Error = "request_timeout"
                });
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                }
            };
            from.Avatar = "https://example.com/avatar.png";
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var performContext = new FakePerformContext(retryCount: 3);
            var sender = env.Activate<AnnouncementSender>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sender.SendAnnouncementMessageAsync(announcement.Messages[0].Id, performContext));

            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal("The announcement message", postedMessage.Text);
            Assert.Equal(room1.PlatformRoomId, postedMessage.Channel);
            Assert.Equal(new Uri("https://example.com/avatar.png"), postedMessage.IconUrl);
            Assert.Equal(from.DisplayName, postedMessage.UserName);
            Assert.Null(announcement.Messages[0].SentDateUtc);
        }

        [Fact]
        public async Task DoesNotThrowExceptionAndCompletesSendingIfRetryAttemptsExhausted()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var from = env.TestData.User;
            env.Clock.Freeze();
            env.SlackApi.AddMessageInfoResponse(
                organization.ApiToken!.Reveal(),
                "The announcement message",
                new MessageResponse
                {
                    Ok = false,
                    Error = "request_timeout"
                });
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
            var announcement = new Announcement
            {
                Text = "The announcement message",
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = organization,
                SourceMessageId = "1234567.32434",
                Messages = new List<AnnouncementMessage>
                {
                    new() { Room = room1 },
                }
            };
            from.Avatar = "https://example.com/avatar.png";
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var performContext = new FakePerformContext(retryCount: 5);
            var sender = env.Activate<AnnouncementSender>();

            await sender.SendAnnouncementMessageAsync(announcement.Messages[0].Id, performContext);

            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal("The announcement message", postedMessage.Text);
            Assert.Equal(room1.PlatformRoomId, postedMessage.Channel);
            Assert.Equal(new Uri("https://example.com/avatar.png"), postedMessage.IconUrl);
            Assert.Equal(from.DisplayName, postedMessage.UserName);
            Assert.Equal(env.Clock.UtcNow, announcement.Messages[0].SentDateUtc);
        }
    }

    public class TheSendReminderAsyncMethod
    {
        [Fact]
        public async Task SendsReminderAndSchedulesBroadcast()
        {
            var env = TestEnvironment.Create();
            var from = env.TestData.User;
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var room1 = await env.CreateRoomAsync();
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
                },
                ScheduledDateUtc = env.Clock.UtcNow.AddHours(1)
            };
            from.Avatar = "https://example.com/avatar.png";
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            var sender = env.Activate<AnnouncementSender>();

            await sender.SendReminderAsync(announcement.Messages[0].Id);

            var postedMessage = Assert.Single(env.SlackApi.PostedMessages);
            Assert.NotNull(postedMessage.Blocks);
            var firstSectionBlock = Assert.IsType<Section>(postedMessage.Blocks[0]);
            Assert.NotNull(firstSectionBlock.Text);
            Assert.Equal($":mega: <https://testorg.example.com/archives/{announcement.SourceRoom.PlatformRoomId}/p123456732434|This message> will be posted in <#{room1.PlatformRoomId}> in *1 hour*.", firstSectionBlock.Text.Text);
            Assert.Equal(from.PlatformUserId, postedMessage.Channel);
            var (job, _, jobId) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            Assert.Equal(nameof(AnnouncementSender.BroadcastAnnouncementAsync), job.Method.Name);
            Assert.Equal(jobId, announcement.ScheduledJobId);
        }
    }
}

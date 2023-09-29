using Abbot.Common.TestHelpers;

public class AnnouncementRepositoryTests
{
    public class TheGetUncompletedAnnouncementsAsyncMethod
    {
        [Fact]
        public async Task ReturnsAnnouncementsThatHaveNotBeenCompletelySent()
        {
            var env = TestEnvironment.Create();
            var now = env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom = await env.CreateRoomAsync();
            await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId1",
                now,
                targetRoom);
            var completed = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId2",
                now.AddMinutes(-1),
                targetRoom);
            await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId3",
                now.AddMinutes(5),
                targetRoom);
            await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId4",
                now.AddMinutes(6),
                targetRoom);
            var futureButCompleted = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId5",
                now.AddMinutes(5),
                targetRoom);
            var futureStartedNotCompleted = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId6",
                now.AddDays(5),
                targetRoom);
            completed.DateCompletedUtc = now;
            futureButCompleted.DateCompletedUtc = now; // Maybe we manually sent it for them.
            futureStartedNotCompleted.DateStartedUtc = now.AddDays(-1);
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<AnnouncementsRepository>();

            var results = await repository.GetUncompletedAnnouncementsAsync(
                page: 1,
                pageSize: 10,
                env.TestData.Organization);

            Assert.Collection(results,
                a => Assert.Equal("MessageId4", a.SourceMessageId),
                a => Assert.Equal("MessageId3", a.SourceMessageId),
                a => Assert.Equal("MessageId1", a.SourceMessageId),
                a => Assert.Equal("MessageId6", a.SourceMessageId));
        }
    }

    public class TheGetCompletedAnnouncementsAsyncMethod
    {
        [Fact]
        public async Task ReturnsAnnouncementsThatHaveBeenSent()
        {
            var env = TestEnvironment.Create();
            var now = env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom = await env.CreateRoomAsync();
            var announcement1 = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId1",
                null,
                targetRoom);
            var announcement2 = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId2",
                now.AddMinutes(-1),
                targetRoom);
            var announcement3 = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId3",
                now.AddMinutes(-3),
                targetRoom);
            announcement1.DateCompletedUtc = now;
            announcement2.DateStartedUtc = now.AddMinutes(-1); // Not completed though.
            announcement3.DateCompletedUtc = now.AddMinutes(-3);
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<AnnouncementsRepository>();

            var results = await repository.GetCompletedAnnouncementsAsync(
                page: 1,
                pageSize: 10,
                7,
                env.TestData.Organization);

            Assert.Collection(results,
                a => Assert.Equal("MessageId1", a.SourceMessageId),
                a => Assert.Equal("MessageId3", a.SourceMessageId));
        }
    }

    public class TheGetAnnouncementFromMessageAsyncMethod
    {
        [Fact]
        public async Task RetrievesAnnouncementFromMessageIdAndChannel()
        {
            var env = TestEnvironment.Create();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId",
                null,
                targetRoom1,
                targetRoom2);
            var repository = env.Activate<AnnouncementsRepository>();

            var result = await repository.GetAnnouncementFromMessageAsync(
                sourceRoom.PlatformRoomId,
                "MessageId",
                env.TestData.Organization);

            Assert.NotNull(result);
            Assert.Equal(announcement.Id, result.Id);
            Assert.Equal(sourceRoom.Id, announcement.SourceRoom.Id);
        }
    }

    public class TheUpdateMessageSendCompletedAsyncMethod
    {
        [Fact]
        public async Task UpdatesAnnouncementMessageSent()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "1658090001.221089",
                null,
                targetRoom1,
                targetRoom2);
            var firstMessage = announcement.Messages[0];
            var lastMessage = announcement.Messages[1];
            var firstMessageDate = env.Clock.UtcNow.AddDays(1);
            var lastMessageDate = env.Clock.UtcNow.AddDays(2);
            var repository = env.Activate<AnnouncementsRepository>();
            await repository.UpdateMessageSendCompletedAsync(
                firstMessage,
                "1658090002.221089",
                null,
                firstMessageDate);
            Assert.Equal(firstMessageDate, firstMessage.SentDateUtc);
            Assert.Equal("1658090002.221089", firstMessage.MessageId);
            Assert.Null(firstMessage.ErrorMessage);

            await repository.UpdateMessageSendCompletedAsync(
                lastMessage,
                "1658090003.221089",
                "Error Will Robinson!",
                lastMessageDate);

            Assert.Equal(lastMessageDate, lastMessage.SentDateUtc);
            Assert.Equal("1658090003.221089", lastMessage.MessageId);
            Assert.Equal("Error Will Robinson!", lastMessage.ErrorMessage);
        }

        [Fact]
        public async Task DoesNotOverwriteExistingMessageIdWithNull()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "1658090001.221089",
                null,
                targetRoom1,
                targetRoom2);
            var firstMessage = announcement.Messages[0];
            var lastMessage = announcement.Messages[1];
            lastMessage.MessageId = "1658090003.221089";
            await env.Db.SaveChangesAsync();
            var firstMessageDate = env.Clock.UtcNow.AddDays(1);
            var lastMessageDate = env.Clock.UtcNow.AddDays(2);
            var repository = env.Activate<AnnouncementsRepository>();
            await repository.UpdateMessageSendCompletedAsync(
                firstMessage,
                "1658090002.221089",
                null,
                firstMessageDate);
            Assert.Equal(firstMessageDate, firstMessage.SentDateUtc);
            Assert.Equal("1658090002.221089", firstMessage.MessageId);
            Assert.Null(firstMessage.ErrorMessage);

            await repository.UpdateMessageSendCompletedAsync(
                lastMessage,
                null,
                "Error Will Robinson!",
                lastMessageDate);

            Assert.Equal(lastMessageDate, lastMessage.SentDateUtc);
            Assert.Equal("1658090003.221089", lastMessage.MessageId);
            Assert.Equal("Error Will Robinson!", lastMessage.ErrorMessage);
        }
    }

    public class TheSetAnnouncementCompletedAsyncMethod
    {
        [Fact]
        public async Task ReturnsTrueWhenAllMessagesSentAndAnnouncementDateCompletedIsNull()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "1658090001.221089",
                null,
                targetRoom1,
                targetRoom2);
            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[1].SentDateUtc = env.Clock.UtcNow;
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<AnnouncementsRepository>();

            var result = await repository.SetAnnouncementCompletedAsync(announcement);

            Assert.True(result);
            Assert.NotNull(announcement.DateCompletedUtc);
        }

        [Fact]
        public async Task ReturnsFalseWhenNotAllMessagesSentAndAnnouncementDateCompletedIsNull()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "1658090001.221089",
                null,
                targetRoom1,
                targetRoom2);
            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<AnnouncementsRepository>();

            var result = await repository.SetAnnouncementCompletedAsync(announcement);

            Assert.False(result);
            Assert.Null(announcement.DateCompletedUtc);
        }

        [Fact]
        public async Task ReturnsFalseWhenAllMessagesSentButAnnouncementDateCompletedIsNotNull()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var sourceRoom = await env.CreateRoomAsync();
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var announcement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "1658090001.221089",
                null,
                targetRoom1,
                targetRoom2);
            announcement.DateCompletedUtc = env.Clock.UtcNow;
            announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
            announcement.Messages[1].SentDateUtc = env.Clock.UtcNow;
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<AnnouncementsRepository>();

            var result = await repository.SetAnnouncementCompletedAsync(announcement);

            Assert.False(result);
            Assert.NotNull(announcement.DateCompletedUtc);
        }
    }
}

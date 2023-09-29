using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack;
using Xunit;

public class RoomPayloadHandlerTests
{
    public class TheOnPlatformEventAsyncMethod
    {
        [Fact]
        public async Task WithRoomUpdateMessageUpdatesRoom()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(name: "test-room");
            var roomEvent = env.CreateFakePlatformEvent(
                new RoomEventPayload(room.PlatformRoomId));
            var token = env.TestData.Organization.ApiToken!.Reveal();
            env.SlackApi.Conversations.AddConversationInfoResponse(token, new ConversationInfo()
            {
                Id = room.PlatformRoomId,
                Name = "test-room-updated",
                NameNormalized = "test-room-updated",
                IsPrivate = true,
                IsChannel = true,
                IsArchived = true,
                IsMember = false,
            });
            var handler = env.Activate<RoomPayloadHandler>();

            await handler.OnPlatformEventAsync(roomEvent);

            await env.ReloadAsync(room);
            Assert.Equal("test-room-updated", room.Name);
            Assert.True(room.Archived);
            Assert.False(room.BotIsMember);
            Assert.False(room.Deleted);
            Assert.Equal(RoomType.PrivateChannel, room.RoomType);
            Assert.Equal(env.Clock.UtcNow, room.LastPlatformUpdate);
        }

        [Fact]
        public async Task WithRoomUpdateMessageMarksRoomDeletedIfSlackReturnsNotFound()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(name: "test-room");
            var roomEvent = env.CreateFakePlatformEvent(new RoomEventPayload(room.PlatformRoomId));
            var token = env.TestData.Organization.ApiToken!.Reveal();
            env.SlackApi.Conversations.AddConversationInfoResponse(token, room.PlatformRoomId, "channel_not_found");
            var handler = env.Activate<RoomPayloadHandler>();

            await handler.OnPlatformEventAsync(roomEvent);

            await env.ReloadAsync(room);
            Assert.Equal("test-room", room.Name);
            Assert.True(room.Deleted);
            Assert.Equal(env.Clock.UtcNow, room.LastPlatformUpdate);
        }
    }
}

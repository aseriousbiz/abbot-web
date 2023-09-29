using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Rooms;
using Serious.Slack;
using Xunit;

namespace Abbot.Web.Library.Tests.Rooms;

public class RoomJoinerTests
{
    public class TheJoinAsyncMethod
    {
        [Fact]
        public async Task UpdatesRoomIfSuccessful()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken.Require().Reveal();
            var room = await env.CreateRoomAsync(botIsMember: false);

            var roomPlatformId = env.IdGenerator.GetSlackChannelId();
            var expectedResponse = env.SlackApi.Conversations.AddConversationInfoResponse(apiToken,
                new() { Id = room.PlatformRoomId });

            var actor = env.TestData.Member;

            var roomJoiner = env.Activate<RoomJoiner>();

            var joinResponse = await roomJoiner.JoinAsync(room, actor);
            Assert.Same(expectedResponse, joinResponse);

            await env.ReloadAsync(room);
            Assert.True(room.BotIsMember);

            await env.AuditLog.AssertMostRecent<AdminAuditEvent>(
                $"Invited @{organization.BotName} to {room.Name} (`{room.PlatformRoomId}`).",
                actor.User, organization);
        }

        [Theory]
        [InlineData("channel_not_found", false, false, true)]
        [InlineData("is_archived", true, true, false)]
        [InlineData("method_not_supported_for_channel_type", false, true, false)]
        [InlineData("another_error", true, false, false)]
        public async Task UpdatesRoomIfUnsuccessful(string error,
            bool expectedManagedConversationsEnabled, bool expectedArchived, bool expectedDeleted)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken.Require().Reveal();
            var room = await env.CreateRoomAsync(
                botIsMember: true,
                managedConversationsEnabled: true,
                deleted: false,
                archived: false);

            var roomPlatformId = env.IdGenerator.GetSlackChannelId();
            var expectedResponse = env.SlackApi.Conversations.AddConversationInfoResponse(apiToken,
                room.PlatformRoomId, error);

            var actor = env.TestData.Member;

            var roomJoiner = env.Activate<RoomJoiner>();

            var joinResponse = await roomJoiner.JoinAsync(room, actor);
            Assert.Same(expectedResponse, joinResponse);

            await env.ReloadAsync(room);
            Assert.True(room.BotIsMember);
            Assert.Equal(expectedManagedConversationsEnabled, room.ManagedConversationsEnabled);
            Assert.Equal(expectedArchived, room.Archived);
            Assert.Equal(expectedDeleted, room.Deleted);

            await env.AuditLog.AssertNoRecent<AuditEventBase>();
        }
    }
}

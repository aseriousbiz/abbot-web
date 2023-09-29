using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.Slack;
using Xunit;

public class OrganizationApiSyncerTests
{
    public class TheUpdateRoomsFromApiAsyncMethod
    {
        [Fact]
        public async Task UpdatesJobBasedOnSlackApi()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<ISlackApiClient>(out var client)
                .Substitute<IConversationsApiClient>(out var conversationsClient)
                .Build();
            client.Conversations.Returns(conversationsClient);
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken.Require().Reveal();
            organization.PlanType = PlanType.Business;
            await env.Db.SaveChangesAsync();
            var rooms = new[]
            {
                // Bot is no longer a member
                await env.CreateRoomAsync(platformRoomId: "C000000001", botIsMember: true, name: "Room 1"),
                // Bot is still a member
                await env.CreateRoomAsync(platformRoomId: "C000000002", botIsMember: true),
                // Bot is now a member
                await env.CreateRoomAsync(platformRoomId: "C000000003", botIsMember: false),
                // Bot is still not a member.
                await env.CreateRoomAsync(platformRoomId: "C000000004", botIsMember: false, name: "Room 4"),
                // Bot is now a member and room is archived.
                await env.CreateRoomAsync(platformRoomId: "C000000005", botIsMember: false),
                // Bot is still not a member, but room is no longer archived.
                await env.CreateRoomAsync(platformRoomId: "C000000006", botIsMember: false, archived: true),
                // Room is deleted.
                await env.CreateRoomAsync(platformRoomId: "C000000007", botIsMember: true, name: "Room 7"),
            };
            client.GetUsersConversationsAsync(
                    apiToken,
                    limit: 1000,
                    user: organization.PlatformBotUserId,
                    types: "public_channel,private_channel",
                    teamId: null,
                    excludeArchived: false,
                    cursor: null)
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfoItem[]
                    {
                        new()
                        {
                            Id = "C000000002",
                            Name = "Room 2"
                        },
                        new()
                        {
                            Id = "C000000003",
                            Name = "Room 3"
                        },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetUsersConversationsAsync(
                    apiToken,
                    limit: 1000,
                    user: organization.PlatformBotUserId,
                    types: "public_channel,private_channel",
                    teamId: null,
                    excludeArchived: false,
                    cursor: "a")
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfoItem[]
                    {
                        new()
                        {
                            Id = "C000000005",
                            Name = "Room 5",
                            IsArchived = true
                        },
                        new()
                        {
                            Id = "C000000006",
                            Name = "Room 6",
                            IsArchived = false
                        },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = null
                    }
                });
            client.Conversations.GetConversationsAsync(
                    apiToken,
                    limit: 1000,
                    types: "public_channel",
                    teamId: null,
                    excludeArchived: true,
                    cursor: null)
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfoItem[]
                    {
                        new()
                        {
                            Id = "C000000007",
                            Name = "Room 7",
                            IsArchived = false,
                        },
                    },
                });
            var job = env.Activate<OrganizationApiSyncer>();

            await job.UpdateRoomsFromApiAsync(organization.Id);

            await env.ReloadAsync(rooms);
            Assert.Equal(
                new[] { false, false, false, false, true, false, false },
                rooms.Select(r => r.Archived.GetValueOrDefault()));

            Assert.Equal(
                new[] { false, true, true, false, true, true, false },
                rooms.Select(r => r.BotIsMember.GetValueOrDefault()));

            var newRoom = await env.Rooms.GetRoomByPlatformRoomIdAsync("C000000007", organization);
            Assert.False(newRoom?.BotIsMember);
        }
    }
}

using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Security;
using Serious.Filters;
using Serious.Slack;

public class RoomRepositoryTests
{
    public class TheGetRoomsByPlatformRoomIdsAsyncMethod
    {
        [Fact]
        public async Task ReturnsLookupResultsForRooms()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<RoomRepository>();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();

            var result = await repo.GetRoomsByPlatformRoomIdsAsync(
                new[] { room1.PlatformRoomId, room2.PlatformRoomId, "C0000000001" },
                env.TestData.Organization);

            Assert.Collection(result,
                r => Assert.Equal((true, room1.PlatformRoomId, room1.Id), (r.Exists, r.PlatformRoomId, r.Room!.Id)),
                r => Assert.Equal((true, room2.PlatformRoomId, room2.Id), (r.Exists, r.PlatformRoomId, r.Room!.Id)),
                r => Assert.Equal((false, "C0000000001", null), (r.Exists, r.PlatformRoomId, r.Room)));
        }
    }

    public class TheGetRoomByPlatformRoomIdAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullIfNoRoomExistsForConversationId()
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync(platformRoomId: "C123");
            var repo = env.Activate<RoomRepository>();

            var room = await repo.GetRoomByPlatformRoomIdAsync("C999", env.TestData.Organization);

            Assert.Null(room);
        }

        [Fact]
        public async Task ReturnsMatchingRoomIfOneExists()
        {
            var env = TestEnvironment.Create();
            var createdRoom = await env.CreateRoomAsync(platformRoomId: "C123ABC");
            var repo = env.Activate<RoomRepository>();

            var room = await repo.GetRoomByPlatformRoomIdAsync("C123ABC", env.TestData.Organization);

            Assert.NotNull(room);
            Assert.Equal(createdRoom.PlatformRoomId, room.PlatformRoomId);
            Assert.Equal(createdRoom.Id, room.Id);
            Assert.Equal(createdRoom.Name, room.Name);
        }
    }

    public class TheGetRoomByNameAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullIfNoRoomExistsForNameInOrganization()
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync(platformRoomId: "C123", name: "test-room");
            await env.CreateRoomAsync("prod-room", org: env.TestData.ForeignOrganization);
            var repo = env.Activate<RoomRepository>();

            var room = await repo.GetRoomByNameAsync("prod-room", env.TestData.Organization);

            Assert.Null(room);
        }

        [Fact]
        public async Task ReturnsMatchingRoomIfOneExists()
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync(platformRoomId: "C123", name: "test-room");
            var repo = env.Activate<RoomRepository>();

            var room = await repo.GetRoomByNameAsync("test-room", env.TestData.Organization);

            Assert.NotNull(room);
            Assert.Equal("C123", room.PlatformRoomId);
            Assert.Equal("test-room", room.Name);
        }
    }

    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task AddsTheProvidedRoom()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<RoomRepository>();
            var createdRoom = new Room
            {
                Name = "test-room",
                PlatformRoomId = "C123",
                Organization = env.TestData.Organization,
            };

            await repo.CreateAsync(createdRoom);

            var savedRoom =
                await repo.GetRoomByPlatformRoomIdAsync(createdRoom.PlatformRoomId, env.TestData.Organization);

            Assert.NotNull(savedRoom);
            Assert.InRange(savedRoom.Id, 1, int.MaxValue);
            Assert.Equal(createdRoom.PlatformRoomId, savedRoom.PlatformRoomId);
            Assert.Equal(env.TestData.Organization.Id, savedRoom.OrganizationId);
        }

        [Fact]
        public async Task RecoversFromDuplicateRoomException()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<RoomRepository>();
            var createdRoom = new Room
            {
                Name = "test-room",
                PlatformRoomId = "C123",
                Organization = env.TestData.Organization,
            };
            var created = await repo.CreateAsync(createdRoom);
            env.Db.ThrowUniqueConstraintViolationOnSave("Rooms", "IX_Rooms_OrganizationId_PlatformRoomId");

            var savedRoom = await repo.CreateAsync(createdRoom);

            Assert.NotNull(savedRoom);
            Assert.Equal(created.Id, savedRoom.Id);
            Assert.Equal(createdRoom.PlatformRoomId, savedRoom.PlatformRoomId);
            Assert.Equal(env.TestData.Organization.Id, savedRoom.OrganizationId);
        }

        [Fact]
        public async Task RecoversFromDuplicateRoomExceptionIfWeReorderIndexes()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<RoomRepository>();
            var createdRoom = new Room
            {
                Name = "test-room",
                PlatformRoomId = "C123",
                Organization = env.TestData.Organization,
            };
            var created = await repo.CreateAsync(createdRoom);
            env.Db.ThrowUniqueConstraintViolationOnSave("Rooms", "IX_Rooms_PlatformRoomId_OrganizationId");

            var savedRoom = await repo.CreateAsync(createdRoom);

            Assert.NotNull(savedRoom);
            Assert.Equal(created.Id, savedRoom.Id);
            Assert.Equal(createdRoom.PlatformRoomId, savedRoom.PlatformRoomId);
            Assert.Equal(env.TestData.Organization.Id, savedRoom.OrganizationId);
        }
    }

    public class TheUpdateAsyncMethod
    {
        [Fact]
        public async Task UpdatesTheProvidedRoom()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<RoomRepository>();
            var existingRoom = new Room
            {
                Name = "test-room",
                PlatformRoomId = "C123",
                Organization = env.TestData.Organization,
            };

            await repo.CreateAsync(existingRoom);
            var savedRoom =
                await repo.GetRoomByPlatformRoomIdAsync(existingRoom.PlatformRoomId, env.TestData.Organization);

            Assert.NotNull(savedRoom);
            Assert.False(existingRoom.ManagedConversationsEnabled);

            existingRoom.ManagedConversationsEnabled = true;
            await repo.UpdateAsync(existingRoom);

            savedRoom = await repo.GetRoomByPlatformRoomIdAsync(existingRoom.PlatformRoomId, env.TestData.Organization);
            Assert.NotNull(savedRoom);
            Assert.True(existingRoom.ManagedConversationsEnabled);
        }
    }

    public class TheUpdateResponseTimesAsyncMethod
    {
        [Theory]
        [InlineData(null, 5)]
        [InlineData(5, null)]
        [InlineData(2, 3)]
        public async Task UpdatesTheResponseTimesForTheProvidedRoom(int? targetMinutes, int? deadlineMinutes)
        {
            var env = TestEnvironment.Create();
            TimeSpan? target = targetMinutes is not null ? TimeSpan.FromMinutes(targetMinutes.Value) : null;
            TimeSpan? deadline = deadlineMinutes is not null ? TimeSpan.FromMinutes(deadlineMinutes.Value) : null;
            var room = await env.CreateRoomAsync();
            var repo = env.Activate<RoomRepository>();

            var result = await repo.UpdateResponseTimesAsync(room, target, deadline, env.TestData.Member);

            Assert.True(result);
            var savedRoom = await repo.GetRoomByPlatformRoomIdAsync(room.PlatformRoomId, env.TestData.Organization);
            Assert.NotNull(savedRoom);
            Assert.Equal(target, savedRoom.TimeToRespond.Warning);
            Assert.Equal(deadline, savedRoom.TimeToRespond.Deadline);
        }

        [Fact]
        public async Task SetsResponseTimesToNull()
        {
            var env = TestEnvironment.Create();
            var target = TimeSpan.FromMinutes(5);
            var deadline = target.Add(TimeSpan.FromMinutes(5));
            var room = await env.CreateRoomAsync();
            var repo = env.Activate<RoomRepository>();
            await repo.UpdateResponseTimesAsync(room, target, deadline, env.TestData.Member);

            var result = await repo.UpdateResponseTimesAsync(room, null, null, env.TestData.Member);

            Assert.True(result);
            var savedRoom = await repo.GetRoomByPlatformRoomIdAsync(room.PlatformRoomId, env.TestData.Organization);
            Assert.NotNull(savedRoom);
            Assert.Null(savedRoom.TimeToRespond.Warning);
            Assert.Null(savedRoom.TimeToRespond.Deadline);
        }

        [Fact]
        public async Task ReturnsFalseWhenResponseTimesAreUnchanged()
        {
            var env = TestEnvironment.Create();
            var target = TimeSpan.FromMinutes(5);
            var deadline = target.Add(TimeSpan.FromMinutes(5));
            var room = await env.CreateRoomAsync();
            var repo = env.Activate<RoomRepository>();
            await repo.UpdateResponseTimesAsync(room, target, deadline, env.TestData.Member);

            var result = await repo.UpdateResponseTimesAsync(room, target, deadline, env.TestData.Member);

            Assert.False(result);
            var savedRoom = await repo.GetRoomByPlatformRoomIdAsync(room.PlatformRoomId, env.TestData.Organization);
            Assert.NotNull(savedRoom);
            Assert.Equal(target, savedRoom.TimeToRespond.Warning);
            Assert.Equal(deadline, savedRoom.TimeToRespond.Deadline);
        }
    }

    public class TheRemoveAsyncMethod
    {
        [Fact]
        public async Task RemovesTheProvidedRoom()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<RoomRepository>();
            var existingRoom = new Room()
            {
                Name = "test-room",
                PlatformRoomId = "C123",
                Organization = env.TestData.Organization,
            };

            await repo.CreateAsync(existingRoom);

            await repo.RemoveAsync(existingRoom);

            var savedRoom =
                await repo.GetRoomByPlatformRoomIdAsync(existingRoom.PlatformRoomId, env.TestData.Organization);

            Assert.Null(savedRoom);
        }
    }

    public class TheGetRoomAsyncMethod
    {
        [Fact]
        public async Task GetsARoom()
        {
            var env = TestEnvironment.Create();
            var expected = new[]
            {
                await env.CreateRoomAsync(persistent: false), await env.CreateRoomAsync(persistent: true),
            };

            Assert.Equal(expected[0].PlatformRoomId, (await env.Rooms.GetRoomAsync(expected[0]))?.PlatformRoomId);
            Assert.Equal(expected[1].PlatformRoomId, (await env.Rooms.GetRoomAsync(expected[1]))?.PlatformRoomId);
        }

        [Fact]
        public async Task ReturnsNullIfNoRoomWithId()
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync(persistent: false);
            await env.CreateRoomAsync(persistent: true);

            Assert.Null(await env.Rooms.GetRoomAsync(new(9999)));
        }
    }

    public class TheGetPersistentRoomsAsyncMethod
    {
        [Theory]
        [InlineData(TrackStateFilter.All, "dfghijklmnop")]   // Order by name only.
        [InlineData(TrackStateFilter.Hubs, "hp")]   // Order by name only.
        [InlineData(TrackStateFilter.Tracked, "fi")]   // Order by name only.
        [InlineData(TrackStateFilter.Untracked, "d")]   // Order by name only.
        [InlineData(TrackStateFilter.Inactive, "jlmkn")] // We sort deleted before archived.
        [InlineData(TrackStateFilter.BotMissing, "go")]
        [InlineData(TrackStateFilter.BotIsMember, "dfhip")]
        public async Task RetrievesPersistentRoomsInTheOrg(TrackStateFilter trackStateFilter, string expectedRoomIds)
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync("b", name: "b", org: env.TestData.ForeignOrganization, persistent: false);
            await env.CreateRoomAsync("a", "a", org: env.TestData.ForeignOrganization, persistent: true);
            await env.CreateRoomAsync("c", "c", org: env.TestData.ForeignOrganization, persistent: true);
            await env.CreateRoomAsync("d", "d", persistent: true, managedConversationsEnabled: false);
            await env.CreateRoomAsync("e", "e", persistent: false);
            await env.CreateRoomAsync("f", "f", persistent: true, managedConversationsEnabled: true, botIsMember: true);
            await env.CreateRoomAsync("g", "g", persistent: true, managedConversationsEnabled: true, botIsMember: false);
            await env.CreateHubAsync("p", "p", config: h => { h.Room.ManagedConversationsEnabled = false; });
            await env.CreateHubAsync("h", "h");
            await env.CreateHubAsync("n", "n", config: h => { h.Room.Archived = true; });
            await env.CreateHubAsync("o", "o", config: h => { h.Room.BotIsMember = false; });
            await env.CreateHubAsync("m", "m", config: h => { h.Room.Deleted = true; });
            await env.CreateRoomAsync("i", "i", persistent: true, managedConversationsEnabled: true, botIsMember: true);
            await env.CreateRoomAsync("j", "j", persistent: true, deleted: true);
            await env.CreateRoomAsync("k", "k", persistent: true, archived: true);
            await env.CreateRoomAsync("l", "l", persistent: true, deleted: true);

            // Load the rooms
            var actual =
                await env.Rooms.GetPersistentRoomsAsync(
                    env.TestData.Organization,
                    filter: default,
                    trackStateFilter,
                    page: 1,
                    pageSize: int.MaxValue);

            // Check the result
            Assert.Equal(expectedRoomIds.ToCharArray(), actual.Select(a => a.PlatformRoomId[0]));
        }
    }

    public class TheGetConversationRoomsAsyncMethod
    {
        [Fact]
        public async Task RetrievesAllPersistentRoomsWithManagedConversationsEnabledInTheOrg()
        {
            var env = TestEnvironment.Create();
            var expected = new[]
            {
                await env.CreateRoomAsync(org: env.TestData.ForeignOrganization, managedConversationsEnabled: true),
                await env.CreateRoomAsync(org: env.TestData.ForeignOrganization,
                    managedConversationsEnabled: false),
                await env.CreateRoomAsync(org: env.TestData.ForeignOrganization, managedConversationsEnabled: true),
                await env.CreateRoomAsync(managedConversationsEnabled: true),
                await env.CreateRoomAsync(managedConversationsEnabled: false),
                await env.CreateRoomAsync(managedConversationsEnabled: true),
            };

            // Load the rooms
            var actual = await env.Rooms.GetConversationRoomsAsync(
                env.TestData.Organization,
                filter: default,
                page: 1,
                pageSize: int.MaxValue);

            // Check the result
            Assert.Equal(
                new[] { expected[3].Id, expected[5].Id },
                actual.Select(a => a.Id).ToArray());
        }

        [Fact]
        public async Task CanFilterByName()
        {
            var env = TestEnvironment.Create();
            var expected = new[]
            {
                await env.CreateRoomAsync(name: "the-danger-room", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "the-cool-room", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "the-boring-room", managedConversationsEnabled: true),
            };

            // Load the rooms
            var actual = await env.Rooms.GetConversationRoomsAsync(
                env.TestData.Organization,
                filter: new FilterList { new("cool") },
                page: 1,
                pageSize: int.MaxValue);

            // Check the result
            Assert.Equal(
                new[] { expected[1].Id },
                actual.Select(a => a.Id).ToArray());
        }

        [Fact]
        public async Task CanPaginate()
        {
            var env = TestEnvironment.Create();
            var expected = new[]
            {
                await env.CreateRoomAsync(name: "a", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "b", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "c", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "d", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "e", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "f", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "g", managedConversationsEnabled: true),
            };

            // Load the rooms
            var page1 = await env.Rooms.GetConversationRoomsAsync(env.TestData.Organization, filter: default, page: 1, pageSize: 3);
            var page2 = await env.Rooms.GetConversationRoomsAsync(env.TestData.Organization, filter: default, page: 2, pageSize: 3);
            var page3 = await env.Rooms.GetConversationRoomsAsync(env.TestData.Organization, filter: default, page: 3, pageSize: 3);
            var page4 = await env.Rooms.GetConversationRoomsAsync(env.TestData.Organization, filter: default, page: 4, pageSize: 3);

            // Check the result
            Assert.Equal(new[] { expected[0].Id, expected[1].Id, expected[2].Id }, page1.Select(a => a.Id).ToArray());
            Assert.False(page1.HasPreviousPage);
            Assert.True(page1.HasNextPage);
            Assert.Equal(new[] { expected[3].Id, expected[4].Id, expected[5].Id }, page2.Select(a => a.Id).ToArray());
            Assert.True(page2.HasPreviousPage);
            Assert.True(page2.HasNextPage);
            Assert.Equal(new[] { expected[6].Id }, page3.Select(a => a.Id).ToArray());
            Assert.True(page3.HasPreviousPage);
            Assert.False(page3.HasNextPage);
            Assert.Empty(page4);
            Assert.True(page4.HasPreviousPage);
            Assert.False(page4.HasNextPage);
        }
    }

    public class TheFindRoomsAsyncMethod
    {
        [Theory]
        [InlineData("", 100, new[] { "a-nice-room", "room-of-requirement", "ʏᴇʟʟɪɴɢ" })]
        [InlineData("", 2, new[] { "a-nice-room", "room-of-requirement" })]
        [InlineData("room", 100, new[] { "room-of-requirement", "a-nice-room" })]
        public async Task FindsRoomsInExpectedOrder(string query, int limit, string[] matchedNames)
        {
            var env = TestEnvironment.Create();

            // Create test rooms
            await env.CreateRoomAsync(name: "room-of-requirement");
            await env.CreateRoomAsync(name: "a-nice-room");
            await env.CreateRoomAsync(name: "ʏᴇʟʟɪɴɢ");

            // Run the query
            var repo = env.Activate<RoomRepository>();
            var results = await repo.FindRoomsAsync(env.TestData.Organization, query, limit);

            Assert.Equal(matchedNames, results.Select(r => r.Name).ToArray());
        }
    }

    public class TheAssignMemberAsyncMethod
    {
        [Fact]
        public async Task AssignsAgentToRoomRole()
        {
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var actor = env.TestData.Member;
            var pikachu = await env.CreateMemberInAgentRoleAsync();
            // This shouldn't happen in Slack, but let's make sure we're filtering this case properly.
            env.TestData.ForeignOrganization.Members.Add(new Member
            {
                User = pikachu.User
            });

            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<RoomRepository>();

            var result = await repository.AssignMemberAsync(room, pikachu, RoomRole.FirstResponder, actor);

            Assert.True(result);
            await env.ReloadAsync(room);
            var firstResponder = Assert.Single(room.GetFirstResponders());
            Assert.Equal(pikachu.Id, firstResponder.Id);
        }

        [Fact]
        public async Task DoesNotAssignsBotMemberToRoomRole()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<RoomRepository>();

            var result = await repository.AssignMemberAsync(room, env.TestData.Abbot, RoomRole.FirstResponder, actor);

            Assert.False(result);
            await env.ReloadAsync(room);
            Assert.Empty(room.GetFirstResponders());
        }

        [Fact]
        public async Task DoesNotAssignsNonAgentToRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberAsync();
            Assert.False(member.MemberRoles.Any(r => r.Role.Name == Roles.Agent));
            var actor = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<RoomRepository>();

            var result = await repository.AssignMemberAsync(room, member, RoomRole.FirstResponder, actor);

            Assert.False(result);
            await env.ReloadAsync(room);
            Assert.Empty(room.GetFirstResponders());
        }
    }

    public class TheSetRoomAssignmentsAsyncMethod
    {
        [Fact]
        public async Task UpdatesRoomAssignmentsToMatchAndIgnoresAbbot()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Member;
            var tyrion = await env.CreateMemberInAgentRoleAsync();
            var danaerys = await env.CreateMemberInAgentRoleAsync();
            var varys = await env.CreateMemberInAgentRoleAsync();
            var arya = await env.CreateMemberInAgentRoleAsync();
            // This shouldn't happen in Slack, but let's make sure we're filtering this case properly.
            env.TestData.ForeignOrganization.Members.Add(new Member
            {
                User = tyrion.User
            });

            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<RoomRepository>();

            await repository.SetRoomAssignmentsAsync(room,
                new[] { varys.User.PlatformUserId, },
                RoomRole.EscalationResponder,
                actor);

            await env.ReloadAsync(room);
            Assert.Empty(room.GetFirstResponders());
            var escalationResponders = room.Assignments.Where(a => a.Role == RoomRole.EscalationResponder);
            Assert.Equal(varys.Id, Assert.Single(escalationResponders).MemberId);

            await repository.SetRoomAssignmentsAsync(room,
                new[]
                {
                    tyrion.User.PlatformUserId, danaerys.User.PlatformUserId, arya.User.PlatformUserId,
                    env.TestData.Abbot.User.PlatformUserId
                },
                RoomRole.FirstResponder,
                actor);

            await env.ReloadAsync(room);
            var firstResponders = room.GetFirstResponders().OrderBy(r => r.Id);
            Assert.Collection(firstResponders,
                r => Assert.Equal(tyrion.Id, r.Id),
                r => Assert.Equal(danaerys.Id, r.Id),
                r => Assert.Equal(arya.Id, r.Id));

            await repository.SetRoomAssignmentsAsync(room,
                new[] { tyrion.User.PlatformUserId, varys.User.PlatformUserId },
                RoomRole.FirstResponder,
                actor);

            await env.ReloadAsync(room);
            Assert.Collection(room.GetFirstResponders(),
                r => Assert.Equal(tyrion.Id, r.Id),
                r => Assert.Equal(varys.Id, r.Id));

            var stillEscalationResponders = room.Assignments.Where(a => a.Role == RoomRole.EscalationResponder);
            Assert.Equal(varys.Id, Assert.Single(stillEscalationResponders).MemberId);
        }
    }

    public class TheSetConversationManagementEnabledAsyncMethod
    {
        [Fact]
        public async Task WhenEnablingSetsLastMessageIdVerified()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<RoomRepository>();

            await repository.SetConversationManagementEnabledAsync(room, true, env.TestData.Member);

            Assert.True(room.ManagedConversationsEnabled);
            var expectedTimestamp = new SlackTimestamp(env.Clock.UtcNow.AddMinutes(5)).ToString();
            var lastMessageIdVerified = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal(expectedTimestamp, lastMessageIdVerified);
        }

        [Fact]
        public async Task WhenDisablingSetsLastMessageIdVerifiedToNull()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var repository = env.Activate<RoomRepository>();

            await repository.SetConversationManagementEnabledAsync(room, false, env.TestData.Member);

            Assert.False(room.ManagedConversationsEnabled);
            var lastMessageIdVerified = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Null(lastMessageIdVerified);
        }
    }

    public class TheRemoveAllRoomAssignmentsForMemberAsyncMethod
    {
        [Fact]
        public async Task RemovesAllRoomAssignmentsForMemberAsync()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room2 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room3 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            await env.Rooms.AssignMemberAsync(room, agent, RoomRole.EscalationResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room2, agent, RoomRole.EscalationResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room3, agent, RoomRole.EscalationResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room2, agent, RoomRole.FirstResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room3, agent, RoomRole.FirstResponder, env.TestData.Abbot);
            var repository = env.Activate<RoomRepository>();

            // I want to make sure our logic for ensuring the `RoomAssignments` property is loaded is working.
            var partiallyLoadedAgent = await env.Db.Members.FindAsync(agent.Id);
            Assert.NotNull(partiallyLoadedAgent);
            await repository.RemoveAllRoomAssignmentsForMemberAsync(partiallyLoadedAgent);

            await env.ReloadAsync(agent);
            Assert.NotNull(agent.RoomAssignments);
            Assert.Empty(agent.RoomAssignments);
        }
    }

    public class TheCreateLinkAsyncMethod
    {
        [Fact]
        public async Task CreatesANewLink()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            Assert.Empty(room.Links);

            await env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);

            await env.ReloadAsync(room);

            Assert.Collection(room.Links,
                l => {
                    Assert.Equal(RoomLinkType.ZendeskOrganization, l.LinkType);
                    Assert.Equal("https://d3v-test.zendesk.com/api/v2/organizations/999.json", l.ExternalId);
                    Assert.Equal("The Derek Zoolander Center for Kids Who Can't Read Good", l.DisplayName);
                    Assert.Equal(env.TestData.Member.Id, l.CreatedById);
                    Assert.Equal(env.Clock.UtcNow, l.Created);
                });
        }

        [Fact]
        public async Task LogsAuditEvent()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            Assert.Empty(room.Links);

            await env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);

            var evt = await env.AuditLog.AssertMostRecent<RoomLinkedEvent>(
                $"Linked the room {room.Name} to a Zendesk Organization \"The Derek Zoolander Center for Kids Who Can't Read Good\".");
            Assert.Equal(RoomLinkType.ZendeskOrganization, evt.LinkType);
            Assert.Equal("https://d3v-test.zendesk.com/api/v2/organizations/999.json", evt.ExternalId);
        }

        [Fact]
        public async Task SendsAnalyticsEvent()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            Assert.Empty(room.Links);

            await env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);

            env.AnalyticsClient.AssertTracked(
                "Room linked",
                AnalyticsFeature.Integrations,
                env.TestData.Member,
                new {
                    link_type = RoomLinkType.ZendeskOrganization.ToString(),
                    room_conversations_enabled = true,
                    room_type = room.RoomType.ToString()
                });
        }
    }

    public class TheRemoveLinkAsyncMethod
    {
        [Fact]
        public async Task RemovesExistingLink()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            Assert.Empty(room.Links);

            await env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);

            await env.ReloadAsync(room);
            Assert.NotEmpty(room.Links);

            await env.Rooms.RemoveLinkAsync(room.Links.Single(), env.TestData.Member);
            await env.ReloadAsync(room);
            Assert.Empty(room.Links);
        }

        [Fact]
        public async Task LogsAuditEvent()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            Assert.Empty(room.Links);
            await env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);
            await env.ReloadAsync(room);
            await env.Rooms.RemoveLinkAsync(room.Links.Single(), env.TestData.Member);

            var evt = await env.AuditLog.AssertMostRecent<RoomUnlinkedEvent>(
                $"Unlinked the room {room.Name} from the Zendesk Organization \"The Derek Zoolander Center for Kids Who Can't Read Good\".");
            Assert.Equal(RoomLinkType.ZendeskOrganization, evt.LinkType);
            Assert.Equal("https://d3v-test.zendesk.com/api/v2/organizations/999.json", evt.ExternalId);
        }

        [Fact]
        public async Task SendsAnalyticsEvent()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            Assert.Empty(room.Links);
            await env.Rooms.CreateLinkAsync(room,
                RoomLinkType.ZendeskOrganization,
                "https://d3v-test.zendesk.com/api/v2/organizations/999.json",
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);
            await env.ReloadAsync(room);
            await env.Rooms.RemoveLinkAsync(room.Links.Single(), env.TestData.Member);

            env.AnalyticsClient.AssertTracked(
                "Room unlinked",
                AnalyticsFeature.Integrations,
                env.TestData.Member,
                new {
                    link_type = RoomLinkType.ZendeskOrganization.ToString(),
                    room_conversations_enabled = true,
                    room_type = room.RoomType.ToString()
                });
        }
    }

    public class TheGetPersistentRoomCountsAsyncMethod
    {
        [Fact]
        public async Task ReturnsRoomCounts()
        {
            var env = await CreateTestEnvironmentWithRooms();
            var repository = env.Activate<RoomRepository>();

            var counts = await repository.GetPersistentRoomCountsAsync(env.TestData.Organization, default);

            var (tracked, untracked, hubs, missingCount, inactiveCount) = counts;
            Assert.Equal((3, 1, 4, 5, 3), (tracked.TotalCount, untracked.TotalCount, hubs.TotalCount, missingCount.TotalCount, inactiveCount.TotalCount));
            Assert.Equal((null, null, null, null, null), (tracked.FilteredCount, untracked.FilteredCount, hubs.FilteredCount, missingCount.FilteredCount, inactiveCount.FilteredCount));
        }

        static async Task<TestEnvironmentWithData> CreateTestEnvironmentWithRooms()
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync(name: "funny-bone", managedConversationsEnabled: true, botIsMember: null);
            await env.CreateRoomAsync(name: "funny-room", managedConversationsEnabled: false, botIsMember: null, archived: true);
            await env.CreateRoomAsync(name: "hilarious-room", managedConversationsEnabled: true, botIsMember: null);
            await env.CreateRoomAsync(name: "funny-business", managedConversationsEnabled: false, botIsMember: false);
            await env.CreateRoomAsync(name: "serious-room", managedConversationsEnabled: true, botIsMember: true);
            await env.CreateRoomAsync(name: "not-funny", managedConversationsEnabled: true, botIsMember: false);
            await env.CreateRoomAsync(name: "le-funny", managedConversationsEnabled: true, botIsMember: true);
            await env.CreateRoomAsync(name: "ha-ha", managedConversationsEnabled: false, botIsMember: true);
            await env.CreateRoomAsync(name: "tada", managedConversationsEnabled: true, botIsMember: true);
            await env.CreateHubAsync("hub-customers", "Chub1");
            await env.CreateHubAsync("hub-funny", "Chub2");
            await env.CreateHubAsync("hub-not-funny", "Chub3");
            await env.CreateHubAsync("hub-serious", "Chub4");
            await env.CreateHubAsync("hub-archived", "Chub5", config: h => h.Room.Archived = true);
            await env.CreateHubAsync("hub-botless", "Chub6", config: h => h.Room.BotIsMember = false);
            await env.CreateHubAsync("hub-deleted", "Chub7", config: h => h.Room.Deleted = true);
            return env;
        }

        [Theory]
        [InlineData("funny", 1, 0, 2, 3, 1)]
        [InlineData("Hub", 0, 0, 4, 1, 2)]
        [InlineData("-", 2, 1, 4, 5, 3)]
        [InlineData("nada", 0, 0, 0, 0, 0)]
        public async Task ReturnsFilteredRoomCounts(string filter, int t, int u, int h, int m, int i)
        {
            var env = await CreateTestEnvironmentWithRooms();
            var repository = env.Activate<RoomRepository>();
            var filterList = FilterParser.Parse(filter);

            var counts = await repository.GetPersistentRoomCountsAsync(env.TestData.Organization, filterList);

            var (tracked, untracked, hubs, missingCount, inactiveCount) = counts;
            Assert.Equal((3, 1, 4, 5, 3), (tracked.TotalCount, untracked.TotalCount, hubs.TotalCount, missingCount.TotalCount, inactiveCount.TotalCount));
            Assert.Equal((t, u, h, m, i), (tracked.FilteredCount, untracked.FilteredCount, hubs.FilteredCount, missingCount.FilteredCount, inactiveCount.FilteredCount));
        }
    }

    public class TheAttachToHubAsyncMethod
    {
        [Fact]
        public async Task ReturnsSuccessIfAlreadyAttachedToSameHub()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync("Croom");
            var hubRoom = await env.CreateRoomAsync("Chub");
            var hub = await env.Hubs.CreateHubAsync("Hub", hubRoom, env.TestData.Member);
            room.Hub = hub;
            room.HubId = hub.Id;
            await env.Db.SaveChangesAsync();
            var rooms = env.Activate<RoomRepository>();

            var result = await rooms.AttachToHubAsync(room, hub, env.TestData.Member);

            Assert.Equal(EntityResultType.Success, result.Type);
        }

        [Fact]
        public async Task ReturnsConflictIfAttachedToDifferentHub()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync("Croom");
            var hubRoom = await env.CreateRoomAsync("Chub");
            var hub = await env.Hubs.CreateHubAsync("Hub", hubRoom, env.TestData.Member);
            var otherHubRoom = await env.CreateRoomAsync("Cother-hub");
            var otherHub = await env.Hubs.CreateHubAsync("Other Hub", otherHubRoom, env.TestData.Member);
            room.Hub = otherHub;
            room.HubId = otherHub.Id;
            await env.Db.SaveChangesAsync();
            var rooms = env.Activate<RoomRepository>();

            var result = await rooms.AttachToHubAsync(room, hub, env.TestData.Member);

            Assert.Equal(EntityResultType.Conflict, result.Type);
        }

        [Fact]
        public async Task AttachesRoomToHub()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync("Croom", name: "the-room");
            var hubRoom = await env.CreateRoomAsync("Chub", name: "the-hub");
            var hub = await env.Hubs.CreateHubAsync("The Hub", hubRoom, env.TestData.Member);
            var rooms = env.Activate<RoomRepository>();

            var result = await rooms.AttachToHubAsync(room, hub, env.TestData.Member);

            Assert.True(result.IsSuccess);

            await env.ReloadAsync(room);
            Assert.Equal(hub.Id, room.HubId);

            var evts = await env.AuditLog.GetRecentActivityAsync(
                env.TestData.Organization,
                new AuditEventType("Room", "AttachedToHub"));
            var evt = Assert.Single(evts);
            Assert.Same(env.TestData.User, evt.Actor);
            Assert.Equal($"Attached #{room.Name} to hub {hub.Name}", evt.Description);
            Assert.Equal(room.Id, evt.EntityId);
            Assert.Equal($$"""{"HubId":{{hub.Id}}}""", evt.SerializedProperties);
        }
    }

    public class TheDetachFromHubAsyncMethod
    {
        [Fact]
        public async Task ReturnsSuccessIfNotAttachedToAnyHub()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync("Croom");
            var hubRoom = await env.CreateRoomAsync("Chub");
            var hub = await env.Hubs.CreateHubAsync("Hub", hubRoom, env.TestData.Member);
            var rooms = env.Activate<RoomRepository>();

            var result = await rooms.DetachFromHubAsync(room, hub, env.TestData.Member);

            Assert.Equal(EntityResultType.Success, result.Type);

            await env.ReloadAsync(room);
            Assert.Null(room.HubId);
        }

        [Fact]
        public async Task ReturnsConflictIfAttachedToDifferentHub()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync("Croom");
            var hubRoom = await env.CreateRoomAsync("Chub");
            var hub = await env.Hubs.CreateHubAsync("Hub", hubRoom, env.TestData.Member);
            var otherHubRoom = await env.CreateRoomAsync("Cother-hub");
            var otherHub = await env.Hubs.CreateHubAsync("Other Hub", otherHubRoom, env.TestData.Member);
            room.Hub = otherHub;
            room.HubId = otherHub.Id;
            await env.Db.SaveChangesAsync();
            var rooms = env.Activate<RoomRepository>();

            var result = await rooms.DetachFromHubAsync(room, hub, env.TestData.Member);

            Assert.Equal(EntityResultType.Conflict, result.Type);

            await env.ReloadAsync(room);
            Assert.Equal(otherHub.Id, room.HubId);
        }

        [Fact]
        public async Task DetachesRoomFromHub()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync("Croom", name: "the-room");
            var hubRoom = await env.CreateRoomAsync("Chub", name: "the-hub");
            var hub = await env.Hubs.CreateHubAsync("The Hub", hubRoom, env.TestData.Member);
            room.Hub = hub;
            room.HubId = hub.Id;
            await env.Db.SaveChangesAsync();
            var rooms = env.Activate<RoomRepository>();

            var result = await rooms.DetachFromHubAsync(room, hub, env.TestData.Member);

            Assert.True(result.IsSuccess);

            await env.ReloadAsync(room);
            Assert.Null(room.HubId);

            var evts = await env.AuditLog.GetRecentActivityAsync(
                env.TestData.Organization,
                new AuditEventType("Room", "DetachedFromHub"));
            var evt = Assert.Single(evts);
            Assert.Same(env.TestData.User, evt.Actor);
            Assert.Equal($"Detached #{room.Name} from hub {hub.Name}", evt.Description);
            Assert.Equal(room.Id, evt.EntityId);
            Assert.Equal($$"""{"HubId":{{hub.Id}}}""", evt.SerializedProperties);
        }
    }
}

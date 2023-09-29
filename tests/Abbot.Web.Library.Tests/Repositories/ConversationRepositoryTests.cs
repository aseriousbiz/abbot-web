using System.Globalization;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Extensions;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack;
using Serious.TestHelpers;

public class ConversationRepositoryTests
{
    static DateTime ToDateTime(int year, int month, int day) => new(year, month, day, 0, 0, 0, 0, DateTimeKind.Utc);

    static DateTimeOffset TestDate(int day, int hour) => new(2022, 01, day, hour, 0, 0, TimeSpan.Zero);

    public class TestData : CommonTestData
    {
        public Room Room { get; private set; } = null!;

        public Room OtherRoom { get; private set; } = null!;

        public Room AssignedRoom { get; private set; } = null!;

        public Conversation[] Conversations { get; private set; } = null!;

        public Conversation[] ConversationsInOtherRoom { get; private set; } = null!;

        public Conversation[] ConversationsInAssignedRoom { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            Room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            OtherRoom = await env.CreateRoomAsync(managedConversationsEnabled: true);
            AssignedRoom = await env.CreateRoomAsync(managedConversationsEnabled: true);

            await env.Rooms.AssignMemberAsync(AssignedRoom, Member, RoomRole.FirstResponder, Member);
            await env.Rooms.AssignMemberAsync(OtherRoom, Member, RoomRole.EscalationResponder, Member);

            Task<Conversation> CreateConversationAsync(Room room, string title, DateTime timestamp, ConversationState initialState = ConversationState.New) =>
                env.CreateConversationAsync(room, title, timestamp, initialState: initialState, createFirstMessageEvent: true);

            Conversations = new[]
            {
                await CreateConversationAsync(Room, "Convo 0", ToDateTime(2000, 01, 01)), // New
                await CreateConversationAsync(Room, "Convo 1", ToDateTime(2001, 01, 01)), // Archived
                await CreateConversationAsync(Room, "Convo 2", ToDateTime(2002, 01, 01)), // New
                await CreateConversationAsync(Room, "Convo 3", ToDateTime(2003, 01, 01)), // NeedsResponse
                await CreateConversationAsync(Room, "Convo 4", ToDateTime(2004, 01, 01)), // Waiting
                await CreateConversationAsync(Room, "Convo 5", ToDateTime(2004, 01, 14)), // Closed
                await CreateConversationAsync(Room,
                    "Convo 6",
                    ToDateTime(2004, 01, 10),
                    initialState: ConversationState.Hidden), // Hidden
            };

            ConversationsInOtherRoom = new[]
            {
                await CreateConversationAsync(OtherRoom, "Convo 6", ToDateTime(2006, 01, 01)), // Closed
            };

            ConversationsInAssignedRoom = new[]
            {
                await CreateConversationAsync(AssignedRoom, "Convo 7", ToDateTime(2006, 01, 01)),
                await CreateConversationAsync(AssignedRoom, "Convo 8", ToDateTime(2006, 01, 14)),
            };

            // Force the conversations into the correct state, we're not testing the business logic of state changes with them
            Conversations[1].State = ConversationState.Archived;
            Conversations[1].ClosedOn = ToDateTime(3000, 01, 01);
            Conversations[1].ArchivedOn = ToDateTime(3001, 01, 01);
            Conversations[1].LastStateChangeOn = ToDateTime(3001, 01, 01);
            Conversations[3].State = ConversationState.NeedsResponse;
            Conversations[4].State = ConversationState.Waiting;
            Conversations[5].State = ConversationState.Closed;
            Conversations[5].ClosedOn = ToDateTime(3000, 01, 01);
            Conversations[5].LastStateChangeOn = ToDateTime(3000, 01, 01);
            env.Db.Conversations.UpdateRange(Conversations);
            ConversationsInAssignedRoom[1].State = ConversationState.Closed;
            ConversationsInAssignedRoom[1].ClosedOn = ToDateTime(3000, 01, 01);
            ConversationsInAssignedRoom[1].LastStateChangeOn = ToDateTime(3000, 01, 01);
            env.Db.Conversations.UpdateRange(ConversationsInAssignedRoom);
            await env.Db.SaveChangesAsync();
        }
    }

    public class TheGetConversationByThreadIdAsyncMethod
    {
        [Fact]
        public async Task ReturnsTheSingleConversationWithMatchingFirstMessageIdInRoom()
        {
            var env = TestEnvironment.Create<TestData>();
            var result = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(result);
            Assert.Equal(env.TestData.Conversations[0].FirstMessageId, result.FirstMessageId);
        }

        [Fact]
        public async Task ReturnsTheSingleConversationWithMatchingThreadIdIdInRoom()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversations[0];
            conversation.ThreadIds.Add("1679918195.660649");
            Assert.NotEqual("1679918195.660649", conversation.FirstMessageId);
            await env.Db.SaveChangesAsync();

            var result = await env.Conversations.GetConversationByThreadIdAsync(
                "1679918195.660649",
                env.TestData.Room);

            Assert.NotNull(result);
            Assert.Equal(conversation.FirstMessageId, result.FirstMessageId);
            Assert.Equal(new[] { conversation.FirstMessageId, "1679918195.660649" }, result.ThreadIds.ToArray());
        }

        [Fact]
        public async Task ReturnsHiddenConversation()
        {
            var env = TestEnvironment.Create<TestData>();

            var result = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[6].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(result);
            Assert.Equal(ConversationState.Hidden, result.State);
            Assert.Equal(env.TestData.Conversations[6].FirstMessageId, result.FirstMessageId);
        }

        [Fact]
        public async Task ReturnsNullIfNoMatch()
        {
            var env = TestEnvironment.Create<TestData>();
            var result =
                await env.Conversations.GetConversationByThreadIdAsync("9999.9999", env.TestData.Room);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullForRoomMismatch()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversations[0];
            var result =
                await env.Conversations.GetConversationByThreadIdAsync(
                    conversation.FirstMessageId,
                    env.TestData.OtherRoom);

            Assert.Null(result);
        }

        [Theory]
        [InlineData(false, "8888.8888", true)]
        [InlineData(true, "7777.7777", true)]
        [InlineData(true, "8888.8888", false)]
        public async Task ReturnsNullIfNotFollowHubThreadOrForHubRoomAndHubThreadMismatch(
            bool followHubThread, string threadId, bool useHubRoom)
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Member;
            var hubRoom = await env.CreateRoomAsync("Chub");
            var hub = await env.Hubs.CreateHubAsync("test-hub", hubRoom, actor);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room, firstMessageId: "7777.7777");
            convo.Hub = hub;
            convo.HubThreadId = "8888.8888";
            await env.Db.SaveChangesAsync();

            using var _ = env.ActivateInNewScope<ConversationRepository>(out var isolated);

            var result = await isolated.GetConversationByThreadIdAsync(
                threadId,
                useHubRoom ? hubRoom : room,
                followHubThread: false);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsTheSingleConversationWithMatchingHubThreadId()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Member;
            var hubRoom = await env.CreateRoomAsync("Chub");
            var hub = await env.Hubs.CreateHubAsync("test-hub", hubRoom, actor);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room, firstMessageId: "7777.7777");
            convo.Hub = hub;
            convo.HubThreadId = "8888.8888";
            await env.Db.SaveChangesAsync();

            using var _ = env.ActivateInNewScope<ConversationRepository>(out var isolated);

            var result = await isolated.GetConversationByThreadIdAsync(
                "8888.8888",
                hubRoom,
                followHubThread: true);

            Assert.Equal(convo.Id, result?.Id);
        }
    }

    public class TheGetConversationAsyncMethod
    {
        [Fact]
        public async Task ReturnsTheSingleConversationWithMatchingId()
        {
            var env = TestEnvironment.Create<TestData>();
            var expectedConvo = env.TestData.Conversations.Last();
            var result = await env.Conversations.GetConversationAsync(expectedConvo.Id);
            Assert.NotNull(result);
            Assert.Equal(expectedConvo.FirstMessageId, result.FirstMessageId);
            Assert.Equal(expectedConvo.Title, result.Title);
        }

        [Fact]
        public async Task ReturnsTheSingleHiddenConversationWithMatchingId()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var expectedConvo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);
            var result = await env.Conversations.GetConversationAsync(expectedConvo.Id);
            Assert.NotNull(result);
            Assert.Equal(expectedConvo.FirstMessageId, result.FirstMessageId);
            Assert.Equal(expectedConvo.Title, result.Title);
        }

        [Fact]
        public async Task ReturnsNullIfNoMatch()
        {
            var env = TestEnvironment.Create<TestData>();
            var result = await env.Conversations.GetConversationAsync(99);
            Assert.Null(result);
        }
    }

    public class TheQueryConversationsAsyncMethod
    {
        // Reminder: The return value is ordered not by title or create date but by Last State Changed date!
        [Theory]
        [InlineData(ConversationStateFilter.All,
            new[] { "Convo 1", "Convo 8", "Convo 5", "Convo 7", "Convo 6", "Convo 4", "Convo 3", "Convo 2", "Convo 0" })]
        [InlineData(ConversationStateFilter.NeedsResponse,
            new[] { "Convo 7", "Convo 6", "Convo 3", "Convo 2", "Convo 0" })]
        [InlineData(ConversationStateFilter.Responded, new[] { "Convo 4" })]
        [InlineData(ConversationStateFilter.Closed, new[] { "Convo 8", "Convo 5" })]
        [InlineData(ConversationStateFilter.Archived, new[] { "Convo 1" })]
        public async Task ReturnsAllConversationsForOrganizationBasedOnStateFilter(ConversationStateFilter stateFilter,
            string[] expectedTitles)
        {
            var env = TestEnvironment.Create<TestData>();
            var query = new ConversationQuery(env.TestData.Organization.Id)
                .WithState(stateFilter);

            var result = await env.Conversations.QueryConversationsWithStatsAsync(
                query,
                env.Clock.UtcNow,
                pageNumber: 0,
                pageSize: int.MaxValue);

            Assert.Equal(9, result.Stats.TotalCount);
            Assert.Equal(expectedTitles, result.Conversations.Select(c => c.Title).ToArray());
        }

        [Theory]
        [InlineData(2, new[] { "Convo 1,Convo 8", "Convo 5,Convo 7", "Convo 6,Convo 4", "Convo 3,Convo 2", "Convo 0" })]
        [InlineData(4, new[] { "Convo 1,Convo 8,Convo 5,Convo 7", "Convo 6,Convo 4,Convo 3,Convo 2", "Convo 0" })]
        [InlineData(int.MaxValue, new[] { "Convo 1,Convo 8,Convo 5,Convo 7,Convo 6,Convo 4,Convo 3,Convo 2,Convo 0" })]
        public async Task ReturnsPaginatedList(int pageSize, string[] pageDefinitions)
        {
            // Can't do arrays of arrays in attributes, so we have to hack it with splitting on ','.
            var expectedPages = pageDefinitions.Select(p => p.Split(',')).ToList();

            var env = TestEnvironment.Create<TestData>();
            var query = new ConversationQuery(env.TestData.Organization.Id);

            int pageNumber = 1;
            for (; pageNumber <= expectedPages.Count; pageNumber++)
            {
                var page =
                    await env.Conversations.QueryConversationsWithStatsAsync(query, DateTime.UtcNow, pageNumber, pageSize);

                Assert.Equal(expectedPages[pageNumber - 1], page.Conversations.Select(c => c.Title).ToArray());
                Assert.Equal(9, page.Stats.TotalCount);
                Assert.Equal(9, page.Conversations.TotalCount);
                Assert.Equal(expectedPages.Count, page.Conversations.TotalPages);
            }

            var overflowPage =
                await env.Conversations.QueryConversationsWithStatsAsync(query, DateTime.UtcNow, pageNumber, pageSize);

            Assert.Equal(0, overflowPage.Conversations.Count);
            Assert.Equal(9, overflowPage.Stats.TotalCount);
            Assert.Equal(9, overflowPage.Conversations.TotalCount);
            Assert.Equal(expectedPages.Count, overflowPage.Conversations.TotalPages);
        }

        [Theory]
        [InlineData(ConversationStateFilter.All, new[] { "Convo 6" })]
        [InlineData(ConversationStateFilter.NeedsResponse, new[] { "Convo 6" })]
        [InlineData(ConversationStateFilter.Closed, new string[0])]
        public async Task ReturnsOnlyConversationsForSpecifiedRoomAndStateFilter(ConversationStateFilter stateFilter,
            string[] expectedTitles)
        {
            var env = TestEnvironment.Create<TestData>();
            var query = new ConversationQuery(env.TestData.Organization.Id)
                .InRooms(env.TestData.OtherRoom.Id)
                .WithState(stateFilter);

            var result =
                await env.Conversations.QueryConversationsWithStatsAsync(query, DateTime.UtcNow, 0, int.MaxValue);

            Assert.Equal(1, result.Stats.TotalCount);
            Assert.Equal(expectedTitles, result.Conversations.OrderBy(c => c.Title).Select(c => c.Title).ToArray());
        }

        [Theory]
        [InlineData(ConversationStateFilter.All)]
        [InlineData(ConversationStateFilter.Archived)]
        [InlineData(ConversationStateFilter.Closed)]
        [InlineData(ConversationStateFilter.Overdue)]
        [InlineData(ConversationStateFilter.Responded)]
        [InlineData(ConversationStateFilter.NeedsResponse)]
        public async Task ReturnsEmptyForOrganizationWithNoConversations(ConversationStateFilter stateFilter)
        {
            var env = TestEnvironment.Create<TestData>();
            var query = new ConversationQuery(99).WithState(stateFilter);
            var result = await env.Conversations.QueryConversationsWithStatsAsync(
                query,
                DateTime.UtcNow,
                0,
                int.MaxValue);

            Assert.Equal(0, result.Stats.TotalCount);
            Assert.Empty(result.Conversations);
        }

        [Fact]
        public async Task ReturnsStatsForProvidedRoom()
        {
            var env = TestEnvironment.Create<TestData>();
            var query = new ConversationQuery(env.TestData.Organization.Id)
                .InRooms(env.TestData.Room.Id);

            var result = await env.Conversations.QueryConversationsWithStatsAsync(
                query,
                ToDateTime(2004, 1, 16),
                0,
                int.MaxValue);

            Assert.Equal(6, result.Stats.TotalCount);
            Assert.Equal(2, result.Stats.CountByState[ConversationState.New]);
            Assert.Equal(1, result.Stats.CountByState[ConversationState.NeedsResponse]);
            Assert.Equal(1, result.Stats.CountByState[ConversationState.Waiting]);
            Assert.Equal(1, result.Stats.CountByState[ConversationState.Closed]);
            Assert.Equal(1, result.Stats.CountByState[ConversationState.Archived]);
        }

        [Fact]
        public async Task ReturnsConversationsAndStatsFromRoomsUserIsAssigned()
        {
            var env = TestEnvironment.Create<TestData>();
            var query = new ConversationQuery(env.TestData.Organization.Id)
                .InRoomsWhereAssigned(env.TestData.Member.Id);

            var result =
                await env.Conversations.QueryConversationsWithStatsAsync(query, ToDateTime(2006, 01, 16), 0, int.MaxValue);

            Assert.Equal(3, result.Stats.TotalCount);
            Assert.Equal(2, result.Stats.CountByState[ConversationState.New]);
            Assert.False(result.Stats.CountByState.ContainsKey(ConversationState.NeedsResponse));
            Assert.False(result.Stats.CountByState.ContainsKey(ConversationState.Waiting));
            Assert.Equal(1, result.Stats.CountByState[ConversationState.Closed]);
            Assert.False(result.Stats.CountByState.ContainsKey(ConversationState.Archived));
            Assert.Equal(new[] { "Convo 6", "Convo 7", "Convo 8" },
                result.Conversations.OrderBy(c => c.Title).Select(c => c.Title).ToArray());
        }

        [Fact]
        public async Task ReturnsOverdueConversationsWhenStateFilterIsOverdue()
        {
            // We're gonna create our own test data for this test because the common test data doesn't really do SLOs.
            var nowUtc = new DateTime(2022, 4, 20);
            var env = TestEnvironment.Create();
            var frRoom = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: true);
            var escalationResponderRoom = await RoomFixture.CreateAsync(env,
                nowUtc,
                frRoom.Member,
                hasSlo: true,
                roomRole: RoomRole.EscalationResponder);

            var roomNoSlo = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: false);
            var unaffiliatedRoom = await RoomFixture.CreateAsync(
                env,
                nowUtc,
                env.TestData.ForeignMember,
                hasSlo: true,
                organization: env.TestData.ForeignOrganization);

            var conversationFixtures = new ConversationFixture[]
            {
                new("Convo 1", frRoom.OkDate), // ConversationState.New
                new("Convo 2", frRoom.OverdueDate, ConversationState.Overdue), // ✅ Visible
                new("Convo 3", frRoom.OverdueDate, ConversationState.Overdue), // ✅ Visible
                new("Convo 4", frRoom.WarningDate), // ConversationState.New
                new("Convo 5", frRoom.WarningDate), // ConversationState.New
                new("Convo 6", frRoom.OverdueDate, State: ConversationState.Closed)
            };

            var followingFixtures = new ConversationFixture[]
            {
                new("Convo 7", escalationResponderRoom.OverdueDate, ConversationState.Overdue), // ✅ Visible
                new("Convo 8", escalationResponderRoom.OkDate),
            };

            var unaffiliated = new ConversationFixture[]
            {
                new("Convo 9", unaffiliatedRoom.OverdueDate, ConversationState.Overdue)
            };

            var noSlo = new ConversationFixture[] { new("Convo 10", roomNoSlo.OverdueDate, ConversationState.Overdue) };
            await frRoom.SetupConversationsAsync(conversationFixtures);
            await escalationResponderRoom.SetupConversationsAsync(followingFixtures);
            await unaffiliatedRoom.SetupConversationsAsync(unaffiliated);
            await roomNoSlo.SetupConversationsAsync(noSlo);

            var query = new ConversationQuery(env.TestData.Organization.Id)
                .WithState(ConversationStateFilter.Overdue);

            var results = await env.Conversations.QueryConversationsWithStatsAsync(query, nowUtc, 0, int.MaxValue);

            Assert.Equal(new[] { "Convo 7", "Convo 3", "Convo 2" },
                results.Conversations.Select(c => c.Title).ToArray());
        }

        [Fact]
        public async Task ReturnsAppropriatelyOrderedConversationsForQueueOrdering()
        {
            // We're gonna create our own test data for this test because the "queue" is complicated.
            var nowUtc = new DateTime(2022, 4, 20);
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var frRoom = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: true);
            var frRoomNoSlo = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: false);
            var escalationResponderRoom = await RoomFixture.CreateAsync(env,
                nowUtc,
                env.TestData.Member,
                hasSlo: true,
                roomRole: RoomRole.EscalationResponder);

            var anotherMember = await env.CreateMemberInAgentRoleAsync();
            var unaffiliatedRoom = await RoomFixture.CreateAsync(env, nowUtc, anotherMember, hasSlo: true);
            await env.Db.SaveChangesAsync();

            // We use synthetic timestamps for testing.
            // We're doing our queries on Day 3, so:
            // Day 1 - Over the critical SLO
            // Day 2 - Over the warning SLO
            // Day 3 - Within the SLO
            // Then we use the hours to order test conversations within each category.

            var frFixtures = new ConversationFixture[]
            {
                new("Convo 1", frRoom.OverdueDate, ConversationState.Overdue), // ✅ Visible
                new("Convo 4", frRoom.WarningDate), // ConversationState.New - // ✅ Visible
                new("Convo 5", frRoom.WarningDate.AddHours(1)), // ConversationState.New - // ✅ Visible
                new("Convo 8", frRoom.OkDate), // ConversationState.New - // ✅ Visible
                new("Convo 9", frRoom.OkDate.AddHours(1)), // ConversationState.New - // ✅ Visible
            };

            var followingFixtures = new ConversationFixture[]
            {
                // New conversations, over Critical SLO, NOT in room the user is FR for - ❎ Not visible
                new("Convo 2", escalationResponderRoom.OverdueDate.AddHours(-1), ConversationState.Overdue)
            };

            // New conversations, in room the user is FR for, but has no SLO or is within SLO - ✅ Visible
            var noSloFixtures = new ConversationFixture[]
            {
                new("Convo 6", frRoomNoSlo.OverdueDate.AddHours(-1)), // ✅ Visible
                new("Convo 7", frRoomNoSlo.OverdueDate.AddDays(1).AddHours(1)), // ✅ Visible
            };

            // New conversations, over Critical SLO, NOT in room the user is FR for - ❎ Not visible
            var unaffiliatedFixtures = new ConversationFixture[]
            {
                new("Convo 3", unaffiliatedRoom.OverdueDate.AddHours(-1), ConversationState.Overdue),
            };

            await frRoom.SetupConversationsAsync(frFixtures);
            await escalationResponderRoom.SetupConversationsAsync(followingFixtures);
            await unaffiliatedRoom.SetupConversationsAsync(unaffiliatedFixtures);
            await frRoomNoSlo.SetupConversationsAsync(noSloFixtures);

            // Now we can test
            var query = ConversationQuery.QueueFor(env.TestData.Member);
            var results =
                await env.Conversations.QueryConversationsWithStatsAsync(query, nowUtc, 0, int.MaxValue);

            Assert.Equal(new[]
                {
                    "Convo 1", // Overdue
                    "Convo 4", "Convo 5", // Warning
                    "Convo 8", "Convo 9", // Ok
                    "Convo 6", "Convo 7", // No SLO
                },
                results.Conversations.Select(c => c.Title).ToArray());

            // Check the stats
            Assert.Equal(1, results.Stats.CountByState[ConversationState.Overdue]); // Only 1 overdue in a room with SLO returned by this query.
            Assert.Equal(6, results.Stats.CountByState.GetValueOrDefault(ConversationState.New));
            Assert.Equal(7, results.Stats.TotalCount);
        }
    }

    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesANewConversation()
        {
            var env = TestEnvironment.Create<TestData>();
            var result = await env.Conversations.CreateAsync(env.TestData.Room,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                "Test Conversation 9",
                env.TestData.Member,
                ToDateTime(1000, 01, 01),
                null);

            var actualConversation =
                await env.Db.Conversations.Where(c => c.Id == result.Id).SingleAsync();

            Assert.Equal(env.TestData.Room.Id, actualConversation.RoomId);
            Assert.Equal(env.TestData.Room.OrganizationId, actualConversation.OrganizationId);
            Assert.Equal(ConversationState.New, actualConversation.State);
            Assert.Equal(1000, actualConversation.Created.Year);
            Assert.Null(actualConversation.ImportedOn);
            Assert.Equal(1000, actualConversation.LastMessagePostedOn.Year);
            Assert.Equal("9999.9999", actualConversation.FirstMessageId);
            Assert.Equal("Test Conversation 9", actualConversation.Title);
            Assert.Equal(env.TestData.Member.Id, actualConversation.StartedById);
            Assert.Equal(1000, actualConversation.LastStateChangeOn.Year);

            var members = await env.Db.ConversationMembers.Where(c => c.ConversationId == result.Id)
                .ToListAsync();

            AssertConversationMembers(members, (env.TestData.Member.Id, 1000, 1000));
            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(actualConversation),
                (env.TestData.Member.Id, 1000, new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9")
                }));

            env.AnalyticsClient.AssertTracked(
                "Conversation created",
                AnalyticsFeature.Conversations,
                env.TestData.Member,
                new {
                    is_guest = false,
                    room_is_shared = env.TestData.Room.Shared.ToString() ?? "null",
                    initial_state = ConversationState.New,
                }
            );
        }

        [Fact]
        public async Task ReturnsExistingWhenDbUpdateExceptionOccurs()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = await env.Conversations.CreateAsync(env.TestData.Room,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                "Test Conversation 9",
                env.TestData.Member,
                ToDateTime(1000, 01, 01),
                null);

            env.Db.ThrowUniqueConstraintViolationOnSave("Conversations", "IX_Conversations_RoomId_FirstMessageId");

            var result = await env.Conversations.CreateAsync(env.TestData.Room,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                "Test Conversation 9",
                env.TestData.Member,
                ToDateTime(1000, 01, 01),
                null);

            Assert.Equal(conversation.Id, result.Id);
        }

        [Theory]
        [InlineData(ConversationState.New)]
        [InlineData(ConversationState.Hidden)]
        public async Task CanCreateConversationInSpecificState(ConversationState initialState)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<ConversationRepository>();
            var firstMessageEvent = new MessagePostedEvent
            {
                MessageId = "9999.9999",
                MessageUrl = new Uri("https://example.com/messages/9")
            };

            var conversation = await repository.CreateAsync(
                room,
                firstMessageEvent,
                title: "title",
                env.TestData.Member,
                env.Clock.UtcNow,
                importedOnUtc: null,
                initialState);

            Assert.Equal(initialState, conversation.State);
            env.AnalyticsClient.AssertTracked(
                "Conversation created",
                AnalyticsFeature.Conversations,
                env.TestData.Member,
                new {
                    is_guest = false,
                    room_is_shared = room.Shared.ToString() ?? "null",
                    initial_state = initialState,
                }
            );
        }

        [Fact]
        public async Task SetsImportedOnIfProvided()
        {
            var env = TestEnvironment.Create<TestData>();
            var result = await env.Conversations.CreateAsync(env.TestData.Room,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                "Test Conversation 9",
                env.TestData.Member,
                ToDateTime(1000, 01, 01),
                ToDateTime(1001, 01, 01));

            var actualConversation = await env.Db.Conversations.Where(c => c.Id == result.Id).SingleAsync();

            Assert.Equal(1000, actualConversation.Created.Year);
            Assert.Equal(1001, actualConversation.ImportedOn?.Year);
        }
    }

    public class TheArchiveAsyncMethod
    {
        [Fact]
        public async Task ArchivesAnActiveConversation()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationAsync(env.TestData.Conversations[0].Id);

            Assert.NotNull(convo);
            Assert.Equal(ConversationState.New, convo.State);
            Assert.Null(convo.ArchivedOn);
            Assert.Null(convo.ClosedOn);

            var stateChange = await env.Conversations.ArchiveAsync(convo, env.TestData.Member, ToDateTime(4000, 01, 01));

            Assert.NotNull(stateChange);
            Assert.Equal(ConversationState.New, stateChange.OldState);
            Assert.Equal(ConversationState.Archived, stateChange.NewState);
            Assert.False(stateChange.Implicit);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());

            convo = await env.Conversations.GetConversationAsync(env.TestData.Conversations[0].Id);

            Assert.NotNull(convo);
            Assert.Equal(ConversationState.Archived, convo.State);
            Assert.Equal(4000, convo.ArchivedOn?.Year);
            Assert.Equal(4000, convo.ClosedOn?.Year);
            Assert.Equal(4000, convo.LastStateChangeOn.Year);

            // Should also add a StatusChanged event
            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 2000, new MessagePostedEvent
                {
                    MessageId = env.TestData.Conversations[0].FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{env.TestData.Conversations[0].FirstMessageId}")
                }),
                (env.TestData.Member.Id, 4000, new StateChangedEvent
                {
                    OldState = ConversationState.New,
                    NewState = ConversationState.Archived,
                    Implicit = false
                }));
        }

        [Fact]
        public async Task NoOpsOnAlreadyArchivedConversation()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationAsync(env.TestData.Conversations[1].Id);
            Assert.NotNull(convo);
            Assert.Equal(ConversationState.Archived, convo.State);
            Assert.Equal(3001, convo.ArchivedOn?.Year);
            Assert.Equal(3001, convo.LastStateChangeOn.Year);

            using var _ = env.Db.RaiseIfSaved();
            var stateChange = await env.Conversations.ArchiveAsync(convo, env.TestData.Member, ToDateTime(2000, 01, 01));

            Assert.Null(stateChange);
            Assert.Empty(env.ConversationPublisher.PublishedStateChanges);

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Archived, convo.State);
            Assert.Equal(3001, convo.ArchivedOn?.Year);
            Assert.Equal(3001, convo.LastStateChangeOn.Year);
        }
    }

    public class TheUnarchiveAsyncMethod
    {
        [Fact]
        public async Task ChangedAnArchivedConversationToClosed()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationAsync(env.TestData.Conversations[1].Id);

            Assert.NotNull(convo);
            Assert.Equal(ConversationState.Archived, convo.State);
            Assert.Equal(3000, convo.ClosedOn?.Year);
            Assert.Equal(3001, convo.ArchivedOn?.Year);

            var stateChange = await env.Conversations.UnarchiveAsync(convo, env.TestData.Member, ToDateTime(3002, 01, 01));

            Assert.NotNull(stateChange);
            Assert.Equal(ConversationState.Archived, stateChange.OldState);
            Assert.Equal(ConversationState.Closed, stateChange.NewState);
            Assert.False(stateChange.Implicit);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());

            convo = await env.Conversations.GetConversationAsync(env.TestData.Conversations[1].Id);

            Assert.NotNull(convo);
            Assert.Equal(ConversationState.Closed, convo.State);
            Assert.Null(convo.ArchivedOn);
            Assert.Equal(3000, convo.ClosedOn?.Year);
            Assert.Equal(3002, convo.LastStateChangeOn.Year);

            // Should also add a StatusChanged event
            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 2001, new MessagePostedEvent
                {
                    MessageId = env.TestData.Conversations[1].FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{env.TestData.Conversations[1].FirstMessageId}")
                }),
                // The original archiving was done manually and didn't generate an event
                (env.TestData.Member.Id, 3002, new StateChangedEvent
                {
                    OldState = ConversationState.Archived,
                    NewState = ConversationState.Closed,
                    Implicit = false
                }));
        }

        [Fact]
        public async Task NoOpsOnAlreadyActiveConversation()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationAsync(env.TestData.Conversations[0].Id);

            Assert.NotNull(convo);
            Assert.Equal(ConversationState.New, convo.State);
            Assert.Null(convo.ArchivedOn);

            using var _ = env.Db.RaiseIfSaved();
            var stateChange = await env.Conversations.UnarchiveAsync(convo, env.TestData.Member, ToDateTime(2000, 01, 01));

            Assert.Null(stateChange);
            Assert.Empty(env.ConversationPublisher.PublishedStateChanges);

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.New, convo.State);
            Assert.Null(convo.ArchivedOn);
            Assert.Equal(convo.Created.Year, convo.LastStateChangeOn.Year);
        }
    }

    public class TheCloseAsyncMethod
    {
        [Theory]
        [InlineData(ConversationState.New)]
        [InlineData(ConversationState.Waiting)]
        [InlineData(ConversationState.NeedsResponse)]
        public async Task ChangedAnActiveConversationToClosed(ConversationState startState)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.CreateConversationAsync(env.TestData.Room,
                createFirstMessageEvent: true,
                timestamp: ToDateTime(1000, 1, 1));

            await env.UpdateAsync(convo, c => c.State = startState);

            var stateChange = await env.Conversations.CloseAsync(convo, env.TestData.Member, ToDateTime(2000, 01, 01));

            Assert.NotNull(stateChange);
            Assert.Equal(startState, stateChange.OldState);
            Assert.Equal(ConversationState.Closed, stateChange.NewState);
            Assert.False(stateChange.Implicit);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Closed, convo.State);
            Assert.Equal(2000, convo.ClosedOn?.Year);
            Assert.Equal(2000, convo.LastStateChangeOn.Year);
            Assert.Null(convo.ArchivedOn);

            // Should also add a StatusChanged event
            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 1000, new MessagePostedEvent
                {
                    MessageId = convo.FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{convo.FirstMessageId}")
                }),
                // The original archiving was done manually and didn't generate an event
                (env.TestData.Member.Id, 2000, new StateChangedEvent
                {
                    OldState = startState,
                    NewState = ConversationState.Closed,
                    Implicit = false
                }));
        }

        [Theory]
        [InlineData(ConversationState.Closed)]
        [InlineData(ConversationState.Archived)]
        public async Task NoOpsOnAlreadyClosedConversation(ConversationState startState)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.CreateConversationAsync(env.TestData.Room);
            convo.State = startState;
            convo.ClosedOn = convo.Created;
            convo.LastStateChangeOn = convo.Created;
            env.Db.Conversations.Update(convo);
            await env.Db.SaveChangesAsync();

            await env.ReloadAsync(convo);
            Assert.Equal(startState, convo.State);

            using var _ = env.Db.RaiseIfSaved();
            var stateChange = await env.Conversations.CloseAsync(convo, env.TestData.Member, ToDateTime(2000, 01, 01));

            Assert.Null(stateChange);
            Assert.Empty(env.ConversationPublisher.PublishedStateChanges);

            await env.ReloadAsync(convo);
            Assert.Equal(startState, convo.State);
            Assert.Equal(convo.Created.Year, convo.ClosedOn?.Year);
            Assert.Equal(convo.Created.Year, convo.LastStateChangeOn.Year);
            Assert.Null(convo.ArchivedOn);
        }

        [Fact]
        public async Task EmitsTheTimeToCloseMetricForEachTimeAConversationIsClosed()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.CreateConversationAsync(env.TestData.Room, timestamp: ToDateTime(1000, 1, 1));
            await env.Conversations.CloseAsync(convo, env.TestData.Member, ToDateTime(1000, 1, 2));
            await env.Conversations.ReopenAsync(convo, env.TestData.Member, ToDateTime(1000, 1, 3));
            await env.Conversations.CloseAsync(convo, env.TestData.Member, ToDateTime(1000, 1, 4));

            var metrics = await env.Db.MetricObservations
                .Where(o => o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToClose)
                .ToListAsync();

            Assert.Equal(
                new[] { TimeSpan.FromDays(1), TimeSpan.FromDays(3) },
                metrics.Select(o => TimeSpan.FromSeconds(o.Value)).ToArray());
        }
    }

    public class TheReopenAsyncMethod
    {
        [Fact]
        public async Task ChangedAClosedConversationToWaiting()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.CreateConversationAsync(env.TestData.Room,
                createFirstMessageEvent: true,
                timestamp: ToDateTime(1000, 1, 1));

            await env.Conversations.CloseAsync(convo, env.TestData.Member, ToDateTime(2000, 01, 01));

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Closed, convo.State);
            Assert.Equal(2000, convo.ClosedOn?.Year);

            var stateChange = await env.Conversations.ReopenAsync(convo, env.TestData.Member, ToDateTime(3000, 01, 01));

            Assert.NotNull(stateChange);
            Assert.Equal(ConversationState.Closed, stateChange.OldState);
            Assert.Equal(ConversationState.Waiting, stateChange.NewState);
            Assert.False(stateChange.Implicit);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Waiting, convo.State);
            Assert.Null(convo.ClosedOn);
            Assert.Equal(3000, convo.LastStateChangeOn.Year);

            // Should also add a StatusChanged event
            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 1000, new MessagePostedEvent
                {
                    MessageId = convo.FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{convo.FirstMessageId}")
                }),
                // The original archiving was done manually and didn't generate an event
                (env.TestData.Member.Id, 2000, new StateChangedEvent
                {
                    OldState = ConversationState.New,
                    NewState = ConversationState.Closed,
                    Implicit = false
                }),
                (env.TestData.Member.Id, 3000, new StateChangedEvent
                {
                    OldState = ConversationState.Closed,
                    NewState = ConversationState.Waiting,
                    Implicit = false
                }));
        }

        [Theory]
        [InlineData(ConversationState.New)]
        [InlineData(ConversationState.Waiting)]
        [InlineData(ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Archived)]
        public async Task NoOpsIfConversationCannotBeReopened(ConversationState startState)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.CreateConversationAsync(env.TestData.Room);
            convo.State = startState;
            env.Db.Conversations.Update(convo);
            await env.Db.SaveChangesAsync();

            await env.ReloadAsync(convo);
            Assert.Equal(startState, convo.State);

            using var _ = env.Db.RaiseIfSaved();
            var stateChange = await env.Conversations.ReopenAsync(convo, env.TestData.Member, ToDateTime(2000, 01, 01));

            Assert.Null(stateChange);
            Assert.Empty(env.ConversationPublisher.PublishedStateChanges);

            await env.ReloadAsync(convo);
            Assert.Equal(startState, convo.State);
            Assert.Null(convo.ClosedOn);
            Assert.Equal(convo.Created.Year, convo.LastStateChangeOn.Year);
        }
    }

    public class TheSnoozeConversationAsyncMethod
    {
        [Theory]
        [InlineData(ConversationState.Unknown)]
        [InlineData(ConversationState.New)]
        [InlineData(ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Overdue)]
        [InlineData(ConversationState.Waiting)]
        [InlineData(ConversationState.Closed)]
        [InlineData(ConversationState.Hidden)]
        public async Task MovesConversationInNonArchivedStateToSnoozedState(
            ConversationState conversationState)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            convo.State = conversationState;
            await env.Db.SaveChangesAsync();

            var actor = env.TestData.Member;
            var lastStateChangeOn = convo.LastStateChangeOn;

            var repository = env.Activate<ConversationRepository>();

            var stateChange = await repository.SnoozeConversationAsync(convo, actor, ToDateTime(5000, 1, 1));

            Assert.NotNull(stateChange);
            Assert.Equal(conversationState, stateChange.OldState);
            Assert.Equal(ConversationState.Snoozed, stateChange.NewState);
            Assert.False(stateChange.Implicit);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Snoozed, convo.State);
            Assert.Equal(lastStateChangeOn, convo.LastStateChangeOn);

            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 2000, new MessagePostedEvent
                {
                    MessageId = env.TestData.Conversations[0].FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{env.TestData.Conversations[0].FirstMessageId}")
                }),
                (env.TestData.Member.Id, 5000, new StateChangedEvent
                {
                    OldState = conversationState,
                    NewState = ConversationState.Snoozed,
                    Implicit = false
                }));
        }

        [Theory]
        [InlineData(ConversationState.Archived)]
        [InlineData(ConversationState.Snoozed)]
        public async Task DoesNotMoveConversationToSnoozedStateIfArchivedOrAlreadySnoozed(
            ConversationState conversationState)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            convo.State = conversationState;
            await env.Db.SaveChangesAsync();

            var actor = env.TestData.Member;
            var lastStateChangeOn = convo.LastStateChangeOn;

            var repository = env.Activate<ConversationRepository>();

            var stateChange = await repository.SnoozeConversationAsync(convo, actor, ToDateTime(5000, 1, 1));

            Assert.Null(stateChange);
            Assert.Empty(env.ConversationPublisher.PublishedStateChanges);

            await env.ReloadAsync(convo);
            Assert.Equal(conversationState, convo.State);
            Assert.Equal(lastStateChangeOn, convo.LastStateChangeOn);

            Assert.Single(convo.Events); // First message
        }
    }

    public class TheWakeConversationAsyncMethod
    {
        [Theory]
        [InlineData(0, ConversationState.New)]
        [InlineData(3, ConversationState.NeedsResponse)]
        public async Task RestoresOriginalStateWhenWakingConversation(int conversationIndex,
            ConversationState initialState)
        {
            var env = TestEnvironment.Create<TestData>();
            var utcNow = env.Clock.Freeze();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[conversationIndex].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            Assert.Equal(initialState, convo.State);
            var repository = env.Activate<ConversationRepository>();

            await repository.SnoozeConversationAsync(convo, env.TestData.Member, utcNow.AddMinutes(30));

            var stateChange = await repository.WakeConversationAsync(convo, env.TestData.Member, utcNow.AddMinutes(45));

            Assert.NotNull(stateChange);
            Assert.Equal(ConversationState.Snoozed, stateChange.OldState);
            Assert.Equal(initialState, stateChange.NewState);
            Assert.False(stateChange.Implicit);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());

            Assert.Equal(initialState, convo.State);
        }

        [Theory]
        [InlineData(ConversationState.Waiting)]
        [InlineData(ConversationState.New)]
        [InlineData(ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Archived)]
        [InlineData(ConversationState.Closed)]
        [InlineData(ConversationState.Overdue)]
        [InlineData(ConversationState.Unknown)]
        public async Task DoesNotMovesConversationToNeedsResponseStateIfItIsNotSnoozed(
            ConversationState conversationState)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            convo.State = conversationState;
            await env.Db.SaveChangesAsync();
            Assert.False(convo.State is ConversationState.Snoozed);
            var repository = env.Activate<ConversationRepository>();

            await repository.WakeConversationAsync(convo, env.TestData.Member, env.Clock.UtcNow);

            Assert.Equal(conversationState, convo.State);
        }
    }

    public class TheUpdateForNewMessageAsyncMethod
    {
        [Fact]
        public async Task AddsMessagePostedEventToConversationTimelineAndDeletesPendingNotifications()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);
            Assert.NotNull(convo);
            await env.Notifications.EnqueueNotifications(convo, new[] { env.TestData.Member });
            Assert.Single(await env.Db.PendingMemberNotifications.ToListAsync());
            Assert.Equal(2000, convo.LastMessagePostedOn.Year);

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999"),
                    ThreadId = convo.FirstMessageId,
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "9999.9999"),
                false);

            Assert.Empty(await env.Db.PendingMemberNotifications.ToListAsync());
            Assert.Equal(ConversationState.Waiting, convo.State);

            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 2000, new MessagePostedEvent
                {
                    MessageId = env.TestData.Conversations[0].FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{env.TestData.Conversations[0].FirstMessageId}"),
                    ThreadId = convo.FirstMessageId,
                }),
                (env.TestData.Member.Id, 3000, new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999"),
                    ThreadId = convo.FirstMessageId,
                }),
                (env.TestData.Member.Id, 3000, new StateChangedEvent
                {
                    OldState = ConversationState.New,
                    NewState = ConversationState.Waiting,
                    Implicit = true,
                    ThreadId = convo.FirstMessageId,
                }));
        }

        [Fact]
        public async Task DoesNotAddDuplicateMessagePostedEventToConversationTimeline()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);
            Assert.NotNull(convo);
            Assert.Equal(2000, convo.LastMessagePostedOn.Year);
            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999"),
                    ThreadId = convo.FirstMessageId,
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "9999.9999"),
                false);


            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999"),
                    ThreadId = convo.FirstMessageId,
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "9999.9999"),
                false);

            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 2000, new MessagePostedEvent
                {
                    MessageId = env.TestData.Conversations[0].FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{env.TestData.Conversations[0].FirstMessageId}"),
                    ThreadId = convo.FirstMessageId,
                }),
                (env.TestData.Member.Id, 3000, new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999"),
                    ThreadId = convo.FirstMessageId,
                }),
                (env.TestData.Member.Id, 3000, new StateChangedEvent
                {
                    OldState = ConversationState.New,
                    NewState = ConversationState.Waiting,
                    Implicit = true,
                    ThreadId = convo.FirstMessageId,
                }));
        }

        [Theory]
        [InlineData(null, "1679957982.927239")]
        [InlineData("1679957691.459409", "1679957691.459409")]
        public async Task AddsMessageFromAnotherThreadToConversationTimeline(string? threadId, string expectedThreadId)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            Assert.Equal(2000, convo.LastMessagePostedOn.Year);

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "1679957982.927239",
                    MessageUrl = new Uri("https://example.com/messages/1679957982.927239"),
                    ThreadId = threadId,
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "1679957982.927239"),
                false);

            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                (env.TestData.Member.Id, 2000, new MessagePostedEvent
                {
                    MessageId = env.TestData.Conversations[0].FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{env.TestData.Conversations[0].FirstMessageId}"),
                    ThreadId = convo.FirstMessageId,
                }),
                (env.TestData.Member.Id, 3000, new MessagePostedEvent
                {
                    MessageId = "1679957982.927239",
                    MessageUrl = new Uri("https://example.com/messages/1679957982.927239"),
                    ThreadId = expectedThreadId,
                }),
                (env.TestData.Member.Id, 3000, new StateChangedEvent
                {
                    OldState = ConversationState.New,
                    NewState = ConversationState.Waiting,
                    Implicit = true,
                    ThreadId = expectedThreadId,
                }));
        }

        [Fact]
        public async Task UpdatesLastMessagePostedOn()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            Assert.Equal(2000, convo.LastMessagePostedOn.Year);

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "1111.2222",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                env.CreateConversationMessage(convo, ToDateTime(2001, 01, 01), messageId: "1111.2222"),
                false);

            convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            Assert.Equal(2001, convo.LastMessagePostedOn.Year);
        }

        [Theory]
        [InlineData(false, "1111.2222", null, null)]
        [InlineData(false, "1111.2222", "1111.2221", "1111.2221")]
        [InlineData(false, "1111.2222", "1111.2223", "1111.2223")]
        [InlineData(true, "1111.2222", null, "1111.2222")]
        [InlineData(true, "1111.2222", "1111.2221", "1111.2222")]
        [InlineData(true, "1111.2222", "1111.2223", "1111.2223")]
        public async Task UpdatesLastSupporteeMessageId(bool isSupportee, string messageId, string? lastMessageId, string? expected)
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            convo.Properties = new() { LastSupporteeMessageId = lastMessageId };
            await env.Db.SaveChangesAsync();

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = messageId,
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                env.CreateConversationMessage(convo, ToDateTime(2001, 01, 01), messageId: messageId),
                isSupportee);

            using var _ = env.ActivateInNewScope<ConversationRepository>(out var isolated);
            convo = await isolated.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            Assert.Equal(expected, convo.Properties.LastSupporteeMessageId);
        }

        [Fact]
        public async Task AddsMemberIfMessageCameFromNewMember()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            Assert.NotNull(convo);
            Assert.Equal(2000, convo.LastMessagePostedOn.Year);

            var newMember = await env.CreateMemberAsync();
            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "1111.2222",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), newMember, messageId: "1111.2222"),
                false);

            convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            AssertConversationMembers(convo?.Members,
                (env.TestData.Member.Id, 2000, 2000),
                (newMember.Id, 3000, 3000));
        }

        [Fact]
        public async Task UpdatesMemberTimestampsIfMemberAlreadyInConversation()
        {
            var env = TestEnvironment.Create<TestData>();

            // Reloading without Members to ensure they're loaded before Update
            var convo = env.TestData.Conversations[0];
            foreach (var cmEntity in env.Db.ChangeTracker.Entries<ConversationMember>())
            {
                cmEntity.State = EntityState.Detached;
            }

            convo.Members.Clear();
            await env.ReloadAsync(convo);
            Assert.Empty(convo.Members);

            Assert.Equal(2000, convo.LastMessagePostedOn.Year);

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "1111.2222",
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "1111.2222"),
                false);

            convo = await env.Conversations.GetConversationByThreadIdAsync(
                env.TestData.Conversations[0].FirstMessageId,
                env.TestData.Room);

            AssertConversationMembers(convo?.Members, (env.TestData.Member.Id, 2000, 3000));
        }

        [Theory]
        [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T01:00:00.0000000Z", "09:00")] // 9am - 6pm PDT
        [InlineData("2023-03-16T16:00:00.0000000Z", "2023-03-17T17:00:00.0000000Z", "10:00")] // 9am - 10am PDT
        [InlineData("1000-01-01T00:00:00.0000000Z", "2000-01-01T00:00:00.0000000Z", "136965.18:00")]
        public async Task EmitsCorrectMetricsWhenMovesFromNewToWaiting(
            string createdDate,
            string lastStateChangedDate,
            string expectedResponseTimeWorkingHours)
        {
            var env = TestEnvironment.Create<TestData>();
            var created = DateTime.Parse(createdDate, null, DateTimeStyles.RoundtripKind);
            var stateChangedOn = DateTime.Parse(lastStateChangedDate, null, DateTimeStyles.RoundtripKind);
            var convo = await env.CreateConversationAsync(env.TestData.Room, "Mock Conversation", created);
            await env.Rooms.AssignMemberAsync(
                convo.Room,
                env.TestData.Member,
                RoomRole.FirstResponder,
                env.TestData.Member);
            env.TestData.Member.TimeZoneId = "America/Los_Angeles";
            env.TestData.Member.WorkingHours = new WorkingHours(new TimeOnly(9, 0), new TimeOnly(18, 0));
            await env.Db.SaveChangesAsync();
            // "Post a message" as the target user
            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                // Yep, we're mocking that the message was posted 1000 years later.
                // Good stress test for our chosen data type?
                env.CreateConversationMessage(convo, stateChangedOn, messageId: "9999.9999"),
                false);

            var ttfr = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToFirstResponse);

            var rt = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToResponse);

            var ttfrWorkingHours = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToFirstResponseDuringCoverage);

            var rtWorkingHours = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToResponseDuringCoverage);

            // Reminder: Because of leap days and such, we do the date math again rather than just assuming something like FromDays(1000*365) (ask me how I know)
            var expected = stateChangedOn - created;
            Assert.Equal(expected, TimeSpan.FromSeconds(ttfr.Value));
            Assert.Equal(expected, TimeSpan.FromSeconds(rt.Value));
            Assert.Equal(ttfrWorkingHours.Value, rtWorkingHours.Value);
            // (364755 days * 8  = 2918040) + (7:45 + 0:15 = 8)
            var expectedWorkingHoursResponseTime = TimeSpan.Parse(expectedResponseTimeWorkingHours);
            Assert.Equal(expectedWorkingHoursResponseTime, TimeSpan.FromSeconds(rtWorkingHours.Value));
        }

        [Fact]
        public async Task EmitsCorrectMetricsWhenMovesFromNeedsResponseToWaiting()
        {
            var env = TestEnvironment.Create<TestData>();
            var convo = await env.CreateConversationAsync(
                env.TestData.Room,
                "Mock Conversation",
                ToDateTime(1000, 1, 1));

            convo.State = ConversationState.NeedsResponse;
            convo.LastStateChangeOn = ToDateTime(1500, 1, 1);
            await env.Db.SaveChangesAsync();
            await env.ReloadAsync(convo);
            var repository = env.Activate<ConversationRepository>();

            // "Post a message" as the target user
            await repository.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                // Yep, we're mocking that the message was posted 500 years later.
                // Good stress test for our chosen data type?
                env.CreateConversationMessage(convo, ToDateTime(2000, 1, 1), messageId: "9999.9999"),
                false);

            var ttfr = await env.Db.MetricObservations
                .SingleOrDefaultAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToFirstResponse);

            Assert.Null(ttfr);
            var rt = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToResponse);

            // Reminder: Because of leap days and such, we do the date math again rather than just assuming something like FromDays(1000*365) (ask me how I know)
            var expected = ToDateTime(2000, 1, 1) - ToDateTime(1500, 1, 1);
            Assert.Equal(expected, TimeSpan.FromSeconds(rt.Value));
        }

        [Fact]
        public async Task EmitsCorrectMetricsWhenMovesFromNewToSnoozedToWaiting()
        {
            var env = TestEnvironment.Create<TestData>();
            var createDate = ToDateTime(1500, 1, 1);
            var responseDate = ToDateTime(2000, 1, 1);
            var convo = await env.CreateConversationAsync(
                env.TestData.Room,
                title: "Mock Conversation",
                timestamp: createDate);

            Assert.Equal(ConversationState.New, convo.State);
            var repository = env.Activate<ConversationRepository>();

            // Snooze the conversation 50 years later.
            await repository.SnoozeConversationAsync(convo, env.TestData.Member, ToDateTime(1550, 1, 1));

            // "Post a message" as the target user
            await repository.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                // Yep, we're mocking that the message was posted 500 years later.
                // Good stress test for our chosen data type?
                env.CreateConversationMessage(convo, responseDate, messageId: "9999.9999"),
                posterIsSupportee: false);

            var ttfr = await env.Db.MetricObservations
                .SingleOrDefaultAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToFirstResponse);

            Assert.NotNull(ttfr);
            var rt = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToResponse);

            // Reminder: Because of leap days and such, we do the date math again rather than just assuming something like FromDays(1000*365) (ask me how I know)
            var expected = responseDate - createDate;
            Assert.Equal(expected, TimeSpan.FromSeconds(ttfr.Value));
            Assert.Equal(expected, TimeSpan.FromSeconds(rt.Value));
        }

        [Fact]
        public async Task EmitsCorrectMetricsWhenMovesFromNewToSnoozedThenAwakenAndThenWaiting()
        {
            var env = TestEnvironment.Create<TestData>();
            var createDate = ToDateTime(1500, 1, 1);
            var responseDate = ToDateTime(2000, 1, 1);
            var convo = await env.CreateConversationAsync(
                env.TestData.Room,
                title: "Mock Conversation",
                timestamp: createDate);

            Assert.Equal(ConversationState.New, convo.State);
            var repository = env.Activate<ConversationRepository>();

            // Snooze the conversation 50 years later.
            await repository.SnoozeConversationAsync(convo, env.TestData.Member, ToDateTime(1550, 1, 1));
            // Wake it up a 100 years after that.
            await repository.WakeConversationAsync(convo, env.TestData.Member, ToDateTime(1650, 1, 1));

            // "Post a message" as the target user
            await repository.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                // Yep, we're mocking that the message was posted 500 years later.
                // Good stress test for our chosen data type?
                env.CreateConversationMessage(convo, responseDate, messageId: "9999.9999"),
                posterIsSupportee: false);

            var ttfr = await env.Db.MetricObservations
                .SingleOrDefaultAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToFirstResponse);

            Assert.NotNull(ttfr);
            var rt = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToResponse);

            // Reminder: Because of leap days and such, we do the date math again rather than just assuming something like FromDays(1000*365) (ask me how I know)
            var expected = responseDate - createDate;
            Assert.Equal(expected, TimeSpan.FromSeconds(ttfr.Value));
            Assert.Equal(expected, TimeSpan.FromSeconds(rt.Value));
        }

        [Fact]
        public async Task EmitsCorrectMetricsWhenMovesFromNeedsResponseToSnoozedToWaiting()
        {
            var env = TestEnvironment.Create<TestData>();
            var createDate = ToDateTime(1500, 1, 1);
            var responseDate = ToDateTime(2000, 1, 1);
            var convo = await env.CreateConversationAsync(
                env.TestData.Room,
                title: "Mock Conversation",
                timestamp: createDate);

            convo.State = ConversationState.NeedsResponse;
            await env.Db.SaveChangesAsync();
            Assert.Equal(ConversationState.NeedsResponse, convo.State);
            var repository = env.Activate<ConversationRepository>();

            // Snooze the conversation 50 years later.
            await repository.SnoozeConversationAsync(convo, env.TestData.Member, ToDateTime(1550, 1, 1));

            // "Post a message" as the target user
            await repository.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                // Yep, we're mocking that the message was posted 500 years later.
                // Good stress test for our chosen data type?
                env.CreateConversationMessage(convo, responseDate, messageId: "9999.9999"),
                posterIsSupportee: false);

            var ttfr = await env.Db.MetricObservations
                .SingleOrDefaultAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToFirstResponse);

            Assert.Null(ttfr);
            var rt = await env.Db.MetricObservations
                .SingleAsync(o =>
                    o.ConversationId == convo.Id && o.Metric == ConversationMetrics.TimeToResponse);

            // Reminder: Because of leap days and such, we do the date math again rather than just assuming something like FromDays(1000*365) (ask me how I know)
            var expected = responseDate - createDate;
            Assert.Equal(expected, TimeSpan.FromSeconds(rt.Value));
        }

        [Theory]
        [InlineData(ConversationState.New, false, true)]
        [InlineData(ConversationState.New, true, false)]
        [InlineData(ConversationState.NeedsResponse, false, true)]
        [InlineData(ConversationState.NeedsResponse, true, false)]
        [InlineData(ConversationState.Overdue, false, true)]
        [InlineData(ConversationState.Overdue, true, false)]
        [InlineData(ConversationState.Closed, true, true)]
        [InlineData(ConversationState.Closed, false, false)]
        public async Task WhenOrgMemberPostsToConversationThatHadBeenNotifiedResetsNotificationDates(
            ConversationState startState,
            bool posterIsCustomer,
            bool isReset)
        {
            var env = TestEnvironment.Create<TestData>();
            // Mock up a conversation in the correct state
            var convo = await env.CreateConversationAsync(
                env.TestData.Room,
                "Mock Conversation",
                ToDateTime(2010, 1, 1));

            convo.State = startState;
            convo.LastStateChangeOn = convo.Created;
            convo.TimeToRespondWarningNotificationSent = ToDateTime(2010, 1, 2);
            env.Db.Conversations.Update(convo);
            await env.Db.SaveChangesAsync();
            var poster = posterIsCustomer
                ? env.TestData.ForeignMember
                : env.TestData.Member;

            var repository = env.Activate<ConversationRepository>();

            await repository.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 1, 1), poster, messageId: "9999.9999"),
                posterIsCustomer);

            // Reload the entity
            await env.ReloadAsync(convo);

            // Check the new state
            if (isReset)
            {
                Assert.Null(convo.TimeToRespondWarningNotificationSent);
            }
            else
            {
                Assert.NotNull(convo.TimeToRespondWarningNotificationSent);
            }
        }

        [Fact]
        public async Task IgnoresHiddenConversationInUnmanagedRoom()
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2004, 01, 01));
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);
            Assert.NotNull(convo);
            Assert.Equal(2004, convo.LastMessagePostedOn.Year);
            Assert.Equal(ConversationState.Hidden, convo.State);

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "9999.9999"),
                true);

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Hidden, convo.State);
            Assert.Equal(2004, convo.LastMessagePostedOn.Year);
        }

        [Theory]
        [InlineData(true, ConversationState.NeedsResponse)]
        [InlineData(false, ConversationState.Waiting)]
        public async Task DoesNotIgnoreHiddenConversationInManagedRoom(
            bool posterIsSupportee,
            ConversationState expectedState)
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2004, 01, 01));
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);
            Assert.NotNull(convo);
            Assert.Equal(2004, convo.LastMessagePostedOn.Year);
            Assert.Equal(ConversationState.Hidden, convo.State);

            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 01, 01), messageId: "9999.9999"),
                posterIsSupportee);

            await env.ReloadAsync(convo);
            Assert.Equal(expectedState, convo.State);
            Assert.Equal(3000, convo.LastMessagePostedOn.Year);
        }

        [Theory]
        [InlineData(ConversationState.New, ConversationState.New)]
        [InlineData(ConversationState.NeedsResponse, ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Overdue, ConversationState.Overdue)]
        [InlineData(ConversationState.Waiting, ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Closed, ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Archived, ConversationState.Archived)]
        public async Task WhenForeignOrgMemberPostsItMakesTheRightStateTransition(
            ConversationState startState,
            ConversationState endState)
        {
            await RunStateTransitionTestAsync(startState, endState, d => d.ForeignMember);
        }

        [Theory]
        [InlineData(ConversationState.New, ConversationState.New)]
        [InlineData(ConversationState.NeedsResponse, ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Overdue, ConversationState.Overdue)]
        [InlineData(ConversationState.Waiting, ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Closed, ConversationState.NeedsResponse)]
        [InlineData(ConversationState.Archived, ConversationState.Archived)]
        public async Task WhenGuestMemberPostsItMakesTheRightStateTransition(
            ConversationState startState,
            ConversationState endState)
        {
            await RunStateTransitionTestAsync(startState, endState, d => d.Guest);
        }

        [Theory]
        [InlineData(ConversationState.New, ConversationState.Waiting)]
        [InlineData(ConversationState.NeedsResponse, ConversationState.Waiting)]
        [InlineData(ConversationState.Overdue, ConversationState.Waiting)]
        [InlineData(ConversationState.Waiting, ConversationState.Waiting)]
        [InlineData(ConversationState.Closed, ConversationState.Closed)]
        [InlineData(ConversationState.Archived, ConversationState.Archived)]
        public async Task WhenHomeOrgMemberPostsItMakesTheRightStateTransition(ConversationState startState,
            ConversationState endState)
        {
            await RunStateTransitionTestAsync(startState, endState, d => d.Member);
        }

        static async Task RunStateTransitionTestAsync(ConversationState startState, ConversationState endState,
            Func<TestData, Member> whoPosted)
        {
            var env = TestEnvironment.Create<TestData>();
            var poster = whoPosted(env.TestData);

            // Mock up a conversation in the correct state
            var convo = await env.CreateConversationAsync(
                env.TestData.Room,
                "Mock Conversation",
                ToDateTime(2010, 1, 1),
                createFirstMessageEvent: true);

            convo.State = startState;
            convo.LastStateChangeOn = convo.Created;
            env.Db.Conversations.Update(convo);
            await env.Db.SaveChangesAsync();

            // "Post a message" as the target user
            await env.Conversations.UpdateForNewMessageAsync(
                convo,
                new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                },
                env.CreateConversationMessage(convo, ToDateTime(3000, 1, 1), poster, messageId: "9999.9999"),
                ConversationTracker.IsSupportee(poster, convo.Room));

            // Reload the entity
            await env.ReloadAsync(convo);

            // Check the new state
            Assert.Equal(endState, convo.State);

            if (endState != startState)
            {
                Assert.Equal(3000, convo.LastStateChangeOn.Year);
                Assert.Equal(endState == ConversationState.Waiting
                        ? 3000
                        : null,
                    convo.FirstResponseOn?.Year);
            }
            else
            {
                Assert.Equal(convo.Created.Year, convo.LastStateChangeOn.Year);
            }

            var expectedTimelineEvents = new (int, int, ConversationEvent)[]
            {
                (env.TestData.Member.Id, 2010, new MessagePostedEvent
                {
                    MessageId = convo.FirstMessageId,
                    MessageUrl = new Uri($"https://example.com/messages/{convo.FirstMessageId}")
                }),
                (poster.Id, 3000, new MessagePostedEvent
                {
                    MessageId = "9999.9999",
                    MessageUrl = new Uri("https://example.com/messages/9999.9999")
                }),
            }.ToList();

            if (endState != startState)
            {
                expectedTimelineEvents.Add(
                    (poster.Id, 3000, new StateChangedEvent
                    {
                        OldState = startState,
                        NewState = endState,
                        Implicit = true
                    }));
            }

            // Check the timeline
            AssertConversationTimeline(await env.Conversations.GetTimelineAsync(convo),
                expectedTimelineEvents.ToArray());
        }
    }

    public class TheGetDailyRollupsAsyncMethod
    {
        static void AssertRollupDates(int expectedYear, int expectedMonth, int expectedDay, ConversationTrendsRollup r)
        {
            var date = new DateOnly(expectedYear, expectedMonth, expectedDay);
            Assert.Equal(date, DateOnly.FromDateTime(r.Start));
            Assert.Equal(TimeOnly.MinValue, TimeOnly.FromDateTime(r.Start));
            Assert.Equal(date, DateOnly.FromDateTime(r.End));
            Assert.Equal(TimeOnly.MaxValue, TimeOnly.FromDateTime(r.End));
        }

        [Fact]
        public async Task ComputesRollupsOfConversationMetricsInRoomsUserIsAssignedAsFirstResponder()
        {
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var assignedRoom = await env.CreateRoomAsync();
            var assignedRoomConvo = await env.CreateConversationAsync(assignedRoom, timestamp: ToDateTime(2022, 1, 3));
            await env.Rooms.AssignMemberAsync(assignedRoom,
                env.TestData.Member,
                RoomRole.FirstResponder,
                env.TestData.Member);

            var notFrRoom = await env.CreateRoomAsync();
            var notFrConvo = await env.CreateConversationAsync(notFrRoom, timestamp: ToDateTime(2022, 1, 4));
            await env.Rooms.AssignMemberAsync(notFrRoom,
                env.TestData.Member,
                RoomRole.EscalationResponder,
                env.TestData.Member);

            var unassignedRoom = await env.CreateRoomAsync();
            var unassignedConvo = await env.CreateConversationAsync(unassignedRoom, timestamp: ToDateTime(2022, 1, 5));

            // Create metrics
            async Task CreateMetrics(Conversation conversation, int value)
            {
                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 1),
                    conversation,
                    ConversationMetrics.TimeToResponse,
                    value);

                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 2),
                    conversation,
                    ConversationMetrics.TimeToResponse,
                    value);

                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 4),
                    conversation,
                    ConversationMetrics.TimeToClose,
                    value);

                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 4),
                    conversation,
                    ConversationMetrics.TimeToClose,
                    value * 2);

                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 7),
                    conversation,
                    ConversationMetrics.TimeToResponse,
                    value);

                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 7),
                    conversation,
                    ConversationMetrics.TimeToResponse,
                    value * 2);

                await env.CreateMetricObservationAsync(ToDateTime(2022, 1, 9),
                    conversation,
                    ConversationMetrics.TimeToResponse,
                    value);
            }

            await CreateMetrics(assignedRoomConvo, 20);

            // These values should _not_ mess up the averages because they should not be selected by the query.
            await CreateMetrics(unassignedConvo, 80);
            await CreateMetrics(notFrConvo, 1000);

            var roomSelector = new AssignedRoomsSelector(env.TestData.Member.Id, RoomRole.FirstResponder);
            // Get the rollup
            var datePeriodSelector = new DatePeriodSelector(7, new LocalDate(2022, 1, 7), DateTimeZone.Utc);
            var tagSelector = TagSelector.All;
            var rollups = await env.Conversations.GetDailyRollupsAsync(
                roomSelector,
                datePeriodSelector,
                tagSelector,
                env.TestData.Organization,
                default);

            // Assert the results
            Assert.Collection(rollups,
                r => {
                    AssertRollupDates(2022, 1, 1, r);
                    Assert.Null(r.AverageTimeToClose);
                    Assert.Equal(20, r.AverageTimeToResponse?.TotalSeconds);
                },
                r => {
                    AssertRollupDates(2022, 1, 2, r);
                    Assert.Null(r.AverageTimeToClose);
                    Assert.Equal(20, r.AverageTimeToResponse?.TotalSeconds);
                },
                r => {
                    AssertRollupDates(2022, 1, 3, r);
                    Assert.Null(r.AverageTimeToClose);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 1, 4, r);
                    Assert.Equal((20 + 40) / 2, r.AverageTimeToClose?.TotalSeconds);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 1, 5, r);
                    Assert.Null(r.AverageTimeToClose);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 1, 6, r);
                    Assert.Null(r.AverageTimeToClose);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 1, 7, r);
                    Assert.Null(r.AverageTimeToClose);
                    Assert.Equal((20 + 40) / 2, r.AverageTimeToResponse?.TotalSeconds);
                });
        }

        [Fact]
        public async Task UsesProvidedTimeZoneToIdentifyDayBoundary()
        {
            var env = TestEnvironment.Create<TestData>();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var assignedRoom = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(assignedRoom,
                env.TestData.Member,
                RoomRole.FirstResponder,
                env.TestData.Member);

            // Create some metric observations straddling the day boundary in UTC, but NOT in America/Vancouver
            // First off, we're in April 2022, so America/Vancouver is in Daylight Savings
            // 11pm UTC on 4/8 is 4pm 4/8 in America/Vancouver
            // 1am UTC on 4/9 is 6pm 4/8 in America/Vancouver
            // So when getting a rollup in UTC, we should get two separate rollups with averages 20 and 40
            // When getting a rollup in America/Vancouver, we should get one rollup with average of 30

            var pdt = DateTimeZoneProviders.Tzdb["America/Vancouver"];

            var date1 = new DateTime(2022, 4, 8, 23, 0, 0, DateTimeKind.Utc);
            Assert.Equal(new DateTime(2022, 4, 8, 16, 0, 0), date1.ToInstant().InZone(pdt).ToDateTimeUnspecified());
            var convo1 = await env.CreateConversationAsync(assignedRoom, timestamp: date1);
            await env.CreateMetricObservationAsync(date1,
                convo1,
                ConversationMetrics.TimeToResponse,
                20);

            var date2 = new DateTime(2022, 4, 9, 1, 0, 0, DateTimeKind.Utc);
            Assert.Equal(new DateTime(2022, 4, 8, 18, 0, 0), date2.ToInstant().InZone(pdt).ToDateTimeUnspecified());
            var convo2 = await env.CreateConversationAsync(assignedRoom, timestamp: date2);
            await env.CreateMetricObservationAsync(date2,
                convo2,
                ConversationMetrics.TimeToResponse,
                40);

            var roomSelector = new AssignedRoomsSelector(env.TestData.Member.Id, RoomRole.FirstResponder);

            var pdtPeriodSelector = new DatePeriodSelector(7, new LocalDate(2022, 4, 9), pdt);
            var tagSelector = TagSelector.All;
            var pdtRollup = await env.Conversations.GetDailyRollupsAsync(
                roomSelector,
                pdtPeriodSelector,
                tagSelector,
                env.TestData.Organization,
                default);

            var utcPeriodSelector = new DatePeriodSelector(7, new LocalDate(2022, 4, 9), DateTimeZone.Utc);
            var utcRollup = await env.Conversations.GetDailyRollupsAsync(
                roomSelector,
                utcPeriodSelector,
                tagSelector,
                env.TestData.Organization,
                default);

            Assert.Collection(utcRollup,
                r => {
                    AssertRollupDates(2022, 4, 3, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 4, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 5, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 6, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 7, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 8, r);
                    Assert.Equal(20, r.AverageTimeToResponse?.TotalSeconds);
                    Assert.Equal(1, r.NewConversations);
                },
                r => {
                    AssertRollupDates(2022, 4, 9, r);
                    Assert.Equal(40, r.AverageTimeToResponse?.TotalSeconds);
                    Assert.Equal(1, r.NewConversations);
                });

            Assert.Collection(pdtRollup,
                r => {
                    AssertRollupDates(2022, 4, 3, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 4, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 5, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 6, r);
                    Assert.Null(r.AverageTimeToResponse);
                },
                r => {
                    AssertRollupDates(2022, 4, 7, r);
                    Assert.Null(r.AverageTimeToResponse);
                    Assert.Null(r.NewConversations);
                },
                r => {
                    AssertRollupDates(2022, 4, 8, r);
                    Assert.Equal(30, r.AverageTimeToResponse?.TotalSeconds);
                    Assert.Equal(2, r.NewConversations);
                },
                r => {
                    AssertRollupDates(2022, 4, 9, r);
                    Assert.Null(r.AverageTimeToResponse);
                    Assert.Null(r.NewConversations);
                });
        }
    }

    public class TheGetConversationsInWarningPeriodForTimeToRespondMethod
    {
        [Fact]
        public async Task ReturnsEmptyCollectionWhenRoomHasNoResponseTimesAndOrgHasNoDefaultResponseTimes()
        {
            var env = TestEnvironment.Create<TestData>();
            var repository = env.Activate<ConversationRepository>();

            var conversations = await repository.GetConversationsInWarningPeriodForTimeToRespond(DateTime.UtcNow);

            Assert.Null(env.TestData.Room.TimeToRespond.Warning);
            Assert.Empty(conversations);
        }

        [Fact]
        public async Task ReturnsConversationsInWarningPeriodForRoomWithCustomResponseTimes()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(env.TestData.ForeignMember, Roles.Agent, env.TestData.Abbot);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            room.TimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2));

            var roomNoSlo = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.FirstResponder, env.TestData.Member);
            var foreignRoom = await env.CreateRoomAsync(org: env.TestData.ForeignOrganization,
                managedConversationsEnabled: true);

            foreignRoom.TimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2));

            await env.Rooms.AssignMemberAsync(
                foreignRoom,
                env.TestData.ForeignMember,
                RoomRole.FirstResponder,
                env.TestData.ForeignMember);

            await env.CreateConversationAsync(room,
                "convo-ignore-0",
                TestDate(1, 0)); // ConversationState.New - In critical breach.

            await env.CreateConversationAsync(room,
                "CONVO-WARN-0",
                TestDate(2, 11)); // ConversationState.New - In warning breach.

            await env.CreateConversationAsync(room,
                "CONVO-WARN-1",
                TestDate(1, 13)); // ConversationState.New - In warning breach.

            await env.CreateConversationAsync(room,
                "convo-ignore-1",
                TestDate(3, 11)); // ConversationState.New - In SLO.

            var archived =
                await env.CreateConversationAsync(room,
                    "convo-ignore-2",
                    TestDate(3, 0)); // ConversationState.Archived - In warning breach.

            archived.State = ConversationState.Archived;
            var needsResponse = await env.CreateConversationAsync(room, "CONVO-WARN-2", TestDate(2, 1));
            needsResponse.State = ConversationState.NeedsResponse;
            needsResponse.LastStateChangeOn = TestDate(2, 0).DateTime; // In warning breach.
            await env.CreateConversationAsync(foreignRoom,
                "CONVO-WARN-3",
                TestDate(2, 2)); // ConversationState.New - In warning breach.

            var notificationSent = await env.CreateConversationAsync(foreignRoom,
                "convo-ignore-3",
                TestDate(3, 0)); // ConversationState.New - In warning breach, but notification sent.

            notificationSent.TimeToRespondWarningNotificationSent = TestDate(1, 0).DateTime;
            var waiting = await env.CreateConversationAsync(foreignRoom,
                "convo-ignore-4",
                TestDate(2, 0)); // ConversationState.NeedsResponse - In warning breach, but waiting on response from customer.

            waiting.State = ConversationState.Waiting;
            await env.CreateConversationAsync(roomNoSlo,
                "convo-ignore-5",
                TestDate(2, 0)); // ConversationState.New - In warning breach, but no SLO.

            var repository = env.Activate<ConversationRepository>();

            var result = await repository.GetConversationsInWarningPeriodForTimeToRespond(TestDate(3, 12).DateTime);

            Assert.Collection(result,
                c0 => Assert.Equal("CONVO-WARN-1", c0.Title),
                c1 => Assert.Equal("CONVO-WARN-2", c1.Title),
                c2 => Assert.Equal("CONVO-WARN-3", c2.Title),
                c3 => Assert.Equal("CONVO-WARN-0", c3.Title));

            Assert.Equal(env.TestData.Member.Id, result[0].Room.Assignments[0].MemberId);
            Assert.Equal(env.TestData.ForeignMember.Id, result[2].Room.Assignments[0].MemberId);
        }

        [Fact]
        public async Task ReturnsConversationInWarningPeriodForOrganizationalDefaultResponseTimes()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(env.TestData.ForeignMember, Roles.Agent, env.TestData.Abbot);
            var roomNoSlo = await env.CreateRoomAsync(managedConversationsEnabled: true);
            env.TestData.Organization.DefaultTimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2));

            await env.Rooms.AssignMemberAsync(roomNoSlo,
                env.TestData.Member,
                RoomRole.FirstResponder,
                env.TestData.Member);

            await env.CreateConversationAsync(roomNoSlo,
                "CONVO-WARN-0",
                TestDate(2, 11)); // ConversationState.New - In warning breach.

            await env.CreateConversationAsync(roomNoSlo,
                "convo-ignore",
                TestDate(3, 11)); // ConversationState.New

            var repository = env.Activate<ConversationRepository>();

            var result = await repository.GetConversationsInWarningPeriodForTimeToRespond(TestDate(3, 12).DateTime);

            var warningConversation = Assert.Single(result);
            Assert.Equal("CONVO-WARN-0", warningConversation.Title);
        }

        [Fact]
        public async Task ReturnsConversationInWarningPeriodForRoomWithResponseTimesIgnoringOrganizationDefaults()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(env.TestData.ForeignMember, Roles.Agent, env.TestData.Abbot);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            room.TimeToRespond = new Threshold<TimeSpan>(TimeSpan.FromDays(1), TimeSpan.FromDays(2));
            env.TestData.Organization.DefaultTimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromDays(100));

            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.FirstResponder, env.TestData.Member);
            await env.CreateConversationAsync(room,
                "convo-ignore-critical",
                TestDate(1, 1)); // ConversationState.New - In critical breach.

            await env.CreateConversationAsync(room,
                "CONVO-WARN-0",
                TestDate(2, 11)); // ConversationState.New - In warning breach.

            await env.CreateConversationAsync(room,
                "convo-ignore",
                TestDate(3, 11)); // ConversationState.New

            var repository = env.Activate<ConversationRepository>();

            var result = await repository.GetConversationsInWarningPeriodForTimeToRespond(TestDate(3, 12).DateTime);

            var warningConversation = Assert.Single(result);
            Assert.Equal("CONVO-WARN-0", warningConversation.Title);
        }
    }

    public class TheGetOverdueConversationsToNotifyAsyncMethod
    {
        [Fact]
        public async Task ReturnsConversationsInCriticalPeriodForRoomWithCustomResponseTimes()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var nowUtc = new DateTime(2022, 4, 20);
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(env.TestData.ForeignMember, Roles.Agent, env.TestData.Abbot);
            var room = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: true);
            var roomNoSlo = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: false);
            var foreignRoom = await RoomFixture.CreateAsync(
                env,
                nowUtc,
                env.TestData.ForeignMember,
                hasSlo: true,
                organization: env.TestData.ForeignOrganization);

            var conversationFixtures = new ConversationFixture[]
            {
                new("CONVO-CRITICAL-0", room.OverdueDate), // ConversationState.New - ✅
                new("convo-ignore-0", room.WarningDate), // ConversationState.New
                new("convo-ignore-1", room.OkDate), // ConversationState.New
                new("convo-ignore-2", room.OverdueDate, ConversationState.Archived),
                new("CONVO-CRITICAL-1", room.OverdueDate, State: ConversationState.NeedsResponse) // ✅
            };

            var foreignConversationFixtures = new ConversationFixture[]
            {
                new("CONVO-CRITICAL-2", foreignRoom.OverdueDate, ConversationState.NeedsResponse), // ✅
                new("convo-ignore-3",
                    foreignRoom.OverdueDate,
                    ConversationState.Overdue), // Already in the overdue state, so ignored.
                new("convo-ignore-4", foreignRoom.OverdueDate, ConversationState.Waiting),
                new("convo-ignore-2", foreignRoom.OverdueDate, ConversationState.Archived)
            };

            var noSloFixtures = new ConversationFixture[]
            {
                new("convo-ignore-5", roomNoSlo.OverdueDate) // ConversationState.New - Overdue, but no SLO.
            };

            await room.SetupConversationsAsync(conversationFixtures);
            await foreignRoom.SetupConversationsAsync(foreignConversationFixtures);
            await roomNoSlo.SetupConversationsAsync(noSloFixtures);

            var repository = env.Activate<ConversationRepository>();

            var result = await repository.GetOverdueConversationsToNotifyAsync(nowUtc);

            Assert.Collection(result,
                c0 => Assert.Equal("CONVO-CRITICAL-0", c0.Title),
                c1 => Assert.Equal("CONVO-CRITICAL-1", c1.Title),
                c2 => Assert.Equal("CONVO-CRITICAL-2", c2.Title));

            Assert.Equal(room.Member.Id, result[0].Room.Assignments[0].MemberId);
            Assert.Equal(foreignRoom.Member.Id, result[2].Room.Assignments[0].MemberId);
        }

        [Fact]
        public async Task ReturnsConversationInCriticalPeriodForOrganizationalDefaultResponseTimes()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(env.TestData.ForeignMember, Roles.Agent, env.TestData.Abbot);
            var roomNoSlo = await env.CreateRoomAsync(managedConversationsEnabled: true);
            env.TestData.Organization.DefaultTimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2));

            await env.Rooms.AssignMemberAsync(roomNoSlo,
                env.TestData.Member,
                RoomRole.FirstResponder,
                env.TestData.Member);

            await env.CreateConversationAsync(roomNoSlo,
                "CONVO-CRITICAL-0",
                TestDate(2, 11)); // ConversationState.New - In critical breach.

            await env.CreateConversationAsync(roomNoSlo,
                "convo-ignore",
                TestDate(3, 11)); // ConversationState.New - In warning breach.

            var repository = env.Activate<ConversationRepository>();

            var result = await repository.GetOverdueConversationsToNotifyAsync(TestDate(4, 12).DateTime);

            var overdueConversation = Assert.Single(result);
            Assert.Equal("CONVO-CRITICAL-0", overdueConversation.Title);
        }

        [Fact]
        public async Task ReturnsConversationInCriticalPeriodForRoomWithResponseTimesIgnoringOrganizationDefaults()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            await env.Roles.AddUserToRoleAsync(env.TestData.ForeignMember, Roles.Agent, env.TestData.Abbot);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            room.TimeToRespond = new Threshold<TimeSpan>(TimeSpan.FromDays(1), TimeSpan.FromDays(2));
            env.TestData.Organization.DefaultTimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2));

            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.FirstResponder, env.TestData.Member);
            await env.CreateConversationAsync(room,
                "CONVO-CRITICAL-0",
                TestDate(2, 11)); // ConversationState.New - In critical breach.

            await env.CreateConversationAsync(room,
                "convo-ignore",
                TestDate(3, 11)); // ConversationState.New - In warning breach.

            var repository = env.Activate<ConversationRepository>();

            var result = await repository.GetOverdueConversationsToNotifyAsync(TestDate(4, 12).DateTime);

            var overdueConversation = Assert.Single(result);
            Assert.Equal("CONVO-CRITICAL-0", overdueConversation.Title);
        }
    }

    public class TheUpdateOverdueConversationAsyncMethod
    {
        [Fact]
        public async Task AddsOverdueState()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var nowUtc = new DateTime(2022, 4, 20);
            var env = TestEnvironment.Create();
            var room = await RoomFixture.CreateAsync(env, nowUtc, env.TestData.Member, hasSlo: true);
            var conversations = await room.SetupConversationsAsync(
                new ConversationFixture("CONVO-CRITICAL-0", room.OverdueDate));

            var conversation = Assert.Single(conversations);
            Assert.Equal(ConversationState.New, conversation.State);
            var repository = env.Activate<ConversationRepository>();

            var stateChange = await repository.UpdateOverdueConversationAsync(conversation, nowUtc, room.Member);

            Assert.NotNull(stateChange);
            Assert.Equal(ConversationState.Overdue, stateChange.NewState);
            Assert.Equal(ConversationState.New, stateChange.OldState);
            Assert.False(stateChange.Implicit);
            Assert.Equal(conversation.FirstMessageId, stateChange.ThreadId);
            Assert.Same(stateChange, env.ConversationPublisher.PublishedStateChanges.Last());
        }

        [Fact]
        public async Task NoOpsIfConversationIsAlreadyOverdue()
        {
            // We're gonna create our own test data for this test because we need that level of control to test it.
            var nowUtc = new DateTime(2022, 4, 20);
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room: room);
            convo.State = ConversationState.Overdue;
            await env.Db.SaveChangesAsync();

            var repository = env.Activate<ConversationRepository>();

            await repository.UpdateOverdueConversationAsync(convo, nowUtc, env.TestData.Member);

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Overdue, convo.State);
            var stateChanges = await env.Db.ConversationEvents
                .OfType<StateChangedEvent>()
                .Where(e => e.ConversationId == convo.Id)
                .ToListAsync();

            Assert.Empty(stateChanges);
        }
    }

    public class TheCreateLinkAsyncMethod
    {
        [Fact]
        public async Task CreatesAConversationLinkOfTheSpecifiedType()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;

            await repository.CreateLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            var loadedConvo = await repository.GetConversationAsync(conversation.Id);
            Assert.NotNull(loadedConvo);
            var link = Assert.Single(loadedConvo.Links);
            Assert.Equal(ConversationLinkType.ZendeskTicket, link.LinkType);
            Assert.Equal("123456", link.ExternalId);
            Assert.Equal(env.TestData.Member.Id, link.CreatedById);
            Assert.Equal(now, link.Created);

            var auditEvent = await env.AuditLog.GetMostRecentLogEntry(env.TestData.Organization);
            var linkEvent = Assert.IsType<ConversationLinkedEvent>(auditEvent);
            Assert.Equal(conversation.Id, linkEvent.EntityId);
            Assert.Equal(ConversationLinkType.ZendeskTicket, linkEvent.LinkType);
            Assert.Equal("123456", linkEvent.ExternalId);
            Assert.Equal(env.TestData.User.Id, linkEvent.ActorId);
            Assert.Equal("Linked the conversation to a Zendesk ticket.", linkEvent.Description);
        }
    }

    public class TheGetConversationLinkAsyncMethod
    {
        [Fact]
        public async Task RetrievesAConversationLinkById()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;

            Id<ConversationLink> id = await repository.CreateLinkAsync(conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            using var _ = env.ActivateInNewScope<ConversationRepository>(out var isolatedRepository);
            var linkedConversation = await isolatedRepository.GetConversationLinkAsync(id);

            Assert.NotNull(linkedConversation);
            Assert.Equal(conversation.Id, linkedConversation.ConversationId);
            Assert.Equal("123456", linkedConversation.ExternalId);

            Assert.NotNull(linkedConversation.Conversation);
            Assert.NotNull(linkedConversation.Conversation.StartedBy);
            Assert.NotNull(linkedConversation.Conversation.Room);
            Assert.NotNull(linkedConversation.Conversation.Room.Organization);
            Assert.NotNull(linkedConversation.Conversation.Room.Assignments);
            Assert.All(linkedConversation.Conversation.Room.Assignments,
                ra => Assert.NotNull(ra.Member.User));
            Assert.NotNull(linkedConversation.Organization);
        }

        [Fact]
        public async Task RetrievesAConversationLinkByExternalIdWithoutOrganization()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;
            await repository.CreateLinkAsync(conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            using var _ = env.ActivateInNewScope<ConversationRepository>(out var isolatedRepository);
            var linkedConversation = await isolatedRepository.GetConversationLinkAsync(
                ConversationLinkType.ZendeskTicket,
                externalId: "123456");

            Assert.NotNull(linkedConversation);
            Assert.Equal(conversation.Id, linkedConversation.ConversationId);
            Assert.Equal("123456", linkedConversation.ExternalId);

            Assert.NotNull(linkedConversation.Conversation);
            Assert.NotNull(linkedConversation.Conversation.StartedBy);
            Assert.NotNull(linkedConversation.Conversation.Room);
            Assert.NotNull(linkedConversation.Conversation.Room.Organization);
            Assert.NotNull(linkedConversation.Conversation.Room.Assignments);
            Assert.All(linkedConversation.Conversation.Room.Assignments,
                ra => Assert.NotNull(ra.Member.User));
            Assert.NotNull(linkedConversation.Organization);
        }

        [Fact]
        public async Task RetrievesAConversationLinkByExternalIdWithOrganizationId()
        {
            var env = TestEnvironment.Create();
            var customer = await env.CreateCustomerAsync(segments: new[] { "TestSegment" });
            Assert.NotEmpty(customer.TagAssignments);
            var room = await env.CreateRoomAsync(customer: customer);
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;
            await repository.CreateLinkAsync(conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            var organizationId = (Id<Organization>)conversation.Organization;

            using var _ = env.ActivateInNewScope<ConversationRepository>(out var isolatedRepository);
            var linkedConversation = await isolatedRepository.GetConversationLinkAsync(
                organizationId,
                ConversationLinkType.ZendeskTicket,
                externalId: "123456");

            Assert.NotNull(linkedConversation);
            Assert.Equal(conversation.Id, linkedConversation.ConversationId);
            Assert.Equal("123456", linkedConversation.ExternalId);

            Assert.NotNull(linkedConversation.Conversation);
            Assert.NotNull(linkedConversation.Conversation.StartedBy);
            Assert.NotNull(linkedConversation.Conversation.Room);
            Assert.NotNull(linkedConversation.Conversation.Room.Organization);
            Assert.NotNull(linkedConversation.Conversation.Room.Assignments);
            Assert.All(linkedConversation.Conversation.Room.Assignments,
                ra => Assert.NotNull(ra.Member.User));
            Assert.NotNull(linkedConversation.Conversation.Room.Customer);
            Assert.NotEmpty(linkedConversation.Conversation.Room.Customer.TagAssignments);
            Assert.All(linkedConversation.Conversation.Room.Customer.TagAssignments,
                ta => Assert.NotNull(ta.Tag));
            Assert.NotNull(linkedConversation.Organization);
        }

        [Fact]
        public async Task ReturnsNullForNoMatchById()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;
            await repository.CreateLinkAsync(conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            var linkedConversation = await repository.GetConversationLinkAsync(NonExistent.ConversationLinkId);

            Assert.Null(linkedConversation);
        }

        [Fact]
        public async Task ReturnsNullForNoMatchWithoutOrganizationId()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;
            await repository.CreateLinkAsync(conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            var linkedConversation = await repository.GetConversationLinkAsync(
                ConversationLinkType.ZendeskTicket,
                externalId: "654321");

            Assert.Null(linkedConversation);
        }

        [Theory]
        [InlineData("123456", TestOrganizationType.Foreign)]
        [InlineData("654321", TestOrganizationType.Home)]
        [InlineData("654321", TestOrganizationType.Foreign)]
        public async Task ReturnsNullForNoMatchWithOrganizationId(string externalId, TestOrganizationType orgType)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            var now = DateTime.UtcNow;
            await repository.CreateLinkAsync(conversation,
                ConversationLinkType.ZendeskTicket,
                "123456",
                env.TestData.Member,
                now);

            var linkedConversation = await repository.GetConversationLinkAsync(
                env.TestData.GetOrganization(orgType),
                ConversationLinkType.ZendeskTicket,
                externalId: externalId);

            Assert.Null(linkedConversation);
        }
    }

    public class TheAssignConversationAsyncMethod
    {
        [Fact]
        public async Task AssignsUserToConversation()
        {
            var env = TestEnvironment.Create();
            var abbot = env.TestData.Abbot;
            var agent = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();

            await repository.AssignConversationAsync(conversation, new[] { agent }, abbot);

            await env.ReloadAsync(conversation);
            var assignee = Assert.Single(conversation.Assignees);
            Assert.Equal(agent.Id, assignee.Id);
            await env.AuditLog.AssertMostRecent<AuditEvent>(
                description: $"assigned {agent.DisplayName} to this conversation.",
                actor: abbot);
        }

        [Fact]
        public async Task ThrowsIfNonAgent()
        {
            var env = TestEnvironment.Create();
            var abbot = env.TestData.Abbot;
            var nonAgent = env.TestData.Member;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.AssignConversationAsync(conversation, new[] { nonAgent }, abbot));
        }

        [Fact]
        public async Task OverwritesAssignedUserToConversation()
        {
            var env = TestEnvironment.Create();
            var abbot = env.TestData.Abbot;
            var agent = await env.CreateMemberInAgentRoleAsync();
            var agent2 = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            await repository.AssignConversationAsync(conversation, new[] { agent }, abbot);

            await repository.AssignConversationAsync(conversation, new[] { agent2 }, abbot);

            await env.ReloadAsync(conversation);
            var assignee = Assert.Single(conversation.Assignees);
            Assert.Equal(agent2.Id, assignee.Id);
        }

        [Fact]
        public async Task ClearsAssignmentsWhenPassedEmptyList()
        {
            var env = TestEnvironment.Create();
            var abbot = env.TestData.Abbot;
            var agent = await env.CreateMemberInAgentRoleAsync();
            await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room: room);
            var repository = env.Activate<ConversationRepository>();
            await repository.AssignConversationAsync(conversation, new[] { agent }, abbot);

            await repository.AssignConversationAsync(conversation, Array.Empty<Member>(), abbot);

            await env.ReloadAsync(conversation);
            Assert.Empty(conversation.Assignees);
        }
    }

    public class TheHasAnyConversationsAsyncMethod
    {
        [Fact]
        public async Task ReturnsFalseWhenNoConversationsIgnoringConversationsInOtherOrgs()
        {
            var env = TestEnvironment.Create();
            var otherRoom = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                org: env.TestData.ForeignOrganization);

            await env.CreateConversationAsync(otherRoom);
            var repository = env.Activate<ConversationRepository>();

            var hasConversations = await repository.HasAnyConversationsAsync(env.TestData.Organization);

            Assert.False(hasConversations);
        }

        [Fact]
        public async Task ReturnsTrueWhenThereIsAConversation()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            await env.CreateConversationAsync(room);
            var repository = env.Activate<ConversationRepository>();

            var hasConversations = await repository.HasAnyConversationsAsync(env.TestData.Organization);

            Assert.True(hasConversations);
        }

        [Fact]
        public async Task ReturnsTrueWhenThereIsAHiddenConversation()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);
            var repository = env.Activate<ConversationRepository>();

            var hasConversations = await repository.HasAnyConversationsAsync(env.TestData.Organization);

            Assert.True(hasConversations);
        }
    }

    public class TheUpdateSummaryAsyncMethod
    {
        [Fact]
        public async Task UpdatesConversationSummaryForConversationAndLastMessagePostedEvent()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<ConversationRepository>();
            var conversation = await env.Conversations.CreateAsync(
                room,
                new MessagePostedEvent
                {
                    MessageId = "1684867372.154399",
                    MessageUrl = new Uri("https://example.com/messages/9"),
                    Metadata = new MessagePostedMetadata
                    {
                        Categories = Array.Empty<Category>(),
                        Text = "Some text",
                        SensitiveValues = Array.Empty<SensitiveValue>(),
                    }.ToJson(),
                },
                "Test Conversation",
                env.TestData.Member,
                env.Clock.UtcNow,
                null);
            env.Clock.AdvanceBy(TimeSpan.FromMinutes(1));
            // Because of race conditions, it's possible to get a duplicate.
            conversation.Events.Add(new MessagePostedEvent
            {
                MessageId = "1684867372.154399",
                MessageUrl = new Uri("https://example.com/messages/9"),
                Metadata = new MessagePostedMetadata
                {
                    Categories = Array.Empty<Category>(),
                    Text = "Some text",
                    SensitiveValues = Array.Empty<SensitiveValue>(),
                }.ToJson(),
            });
            await env.Db.SaveChangesAsync();
            env.Clock.AdvanceBy(TimeSpan.FromMinutes(1));

            await repository.UpdateSummaryAsync(
                conversation,
                conversation.FirstMessageId,
                new SummarizationResult
                {
                    Replacements = new Dictionary<string, SecretString>(),
                    Summary = "The summary",
                    ReasonedActions = Array.Empty<Reasoned<string>>(),
                    RawCompletion = "null",
                    PromptTemplate = "null",
                    Prompt = "null",
                    Temperature = 0,
                    TokenUsage = new TokenUsage(1, 2, 3),
                    Model = "unit-test",
                    ProcessingTime = default,
                    Directives = Array.Empty<Directive>(),
                    UtcTimestamp = env.Clock.UtcNow,
                },
                new ConversationProperties { Summary = "The summary" },
                SlackTimestamp.Parse("1684867372.154399"),
                env.TestData.Member,
                env.Clock.UtcNow);

            Assert.Equal("The summary", conversation.Summary);
            var messagePostedEvents = conversation.Events.OfType<MessagePostedEvent>().OrderBy(e => e.Created).ToList();
            ;
            Assert.Equal(2, messagePostedEvents.Count);
            var firstMessagePostedEvent = messagePostedEvents[0];
            Assert.Empty(firstMessagePostedEvent.DeserializeMetadata()?.SummarizationResult?.Summary ?? "");
            var lastMessagePostedEvent = messagePostedEvents[^1];
            var lastMessagePostedMetadata = lastMessagePostedEvent.DeserializeMetadata();
            Assert.NotNull(lastMessagePostedMetadata?.SummarizationResult);
            Assert.Equal(conversation.Summary, lastMessagePostedMetadata.SummarizationResult.Summary);
        }
    }

    public class TheAttachConversationToHubAsyncMethod
    {
        [Fact]
        public async Task FailsIfConversationAlreadyAttachedToAHub()
        {
            var env = TestEnvironment.Create();
            var hubRoom = await env.CreateRoomAsync();
            var hub = await env.Hubs.CreateHubAsync("test-hub", hubRoom, env.TestData.Member);
            var convoRoom = await env.CreateRoomAsync();
            var repository = env.Activate<ConversationRepository>();

            // Fake the conversation already being attached to a hub
            var conversation = await env.CreateConversationAsync(room: convoRoom);
            conversation.HubId = 42;
            await env.Db.SaveChangesAsync();

            var result =
                await repository.AttachConversationToHubAsync(conversation,
                    hub,
                    "the-thread-id",
                    new Uri("https://example.com/the-thread"),
                    env.TestData.Member,
                    env.Clock.UtcNow);

            Assert.Equal(EntityResultType.Conflict, result.Type);
        }

        [Fact]
        public async Task SetsMetadataAndLogsAuditEventIfConversationNotAlreadyAttachedToHub()
        {
            var env = TestEnvironment.Create();
            var hubRoom = await env.CreateRoomAsync();
            var hub = await env.Hubs.CreateHubAsync("test-hub", hubRoom, env.TestData.Member);
            var convoRoom = await env.CreateRoomAsync();
            var repository = env.Activate<ConversationRepository>();
            var conversation = await env.CreateConversationAsync(room: convoRoom);

            var result =
                await repository.AttachConversationToHubAsync(conversation,
                    hub,
                    "the-thread-id",
                    new("https://example.com/the-thread"),
                    env.TestData.Member,
                    env.Clock.UtcNow);

            Assert.Equal(EntityResultType.Success, result.Type);

            await env.ReloadAsync(conversation);
            Assert.Equal(hub.Id, conversation.HubId);
            Assert.Equal("the-thread-id", conversation.HubThreadId);

            var evt = await env.AuditLog.AssertMostRecent<AuditEvent>(
                description: $"attached this conversation to the '{hub.Name}' Hub.",
                actor: env.TestData.Member);

            var properties = evt.ReadProperties<IDictionary<string, object>>();
            Assert.NotNull(properties);
            Assert.Equal(
                new (string, object)[] { ("HubId", (long)hub.Id), ("HubThreadId", "the-thread-id") },
                properties.ToOrderedPairs());
        }

        [Fact]
        public async Task AddsConversationTimelineEvent()
        {
            var env = TestEnvironment.Create();
            var hubRoom = await env.CreateRoomAsync();
            var hub = await env.Hubs.CreateHubAsync("test-hub", hubRoom, env.TestData.Member);
            var convoRoom = await env.CreateRoomAsync();
            var repository = env.Activate<ConversationRepository>();
            var conversation = await env.CreateConversationAsync(room: convoRoom);

            var result =
                await repository.AttachConversationToHubAsync(conversation,
                    hub,
                    "the-thread-id",
                    new("https://example.com/the-thread"),
                    env.TestData.Member,
                    env.Clock.UtcNow);

            Assert.Equal(EntityResultType.Success, result.Type);

            var timeline = await env.Conversations.GetTimelineAsync(conversation);
            var attachEvent = Assert.IsType<AttachedToHubEvent>(timeline.LastOrDefault());
            Assert.Equal(hub.Id, attachEvent.HubId);
            Assert.Equal("the-thread-id", attachEvent.MessageId);
            Assert.Equal(new("https://example.com/the-thread"), attachEvent.MessageUrl);
        }
    }

    static void AssertConversationMembers(IEnumerable<ConversationMember>? actualMembers,
        params (int MemberId, int JoinedYear, int LastPostedYear)[] expecteds)
    {
        Assert.NotNull(actualMembers);
        var inspectors = expecteds.Select(
            expected => new Action<ConversationMember>(actual => {
                Assert.Equal(expected.MemberId, actual.MemberId);
                Assert.Equal(expected.JoinedYear, actual.JoinedConversationAt.Year);
                Assert.Equal(expected.LastPostedYear, actual.LastPostedAt.Year);
            })).ToArray();

        Assert.Collection(actualMembers, inspectors);
    }

    static void AssertConversationTimeline(
        IEnumerable<ConversationEvent> actualEvents,
        params (int MemberId, int Year, ConversationEvent ConversationEvent)[] expecteds)
    {
        var inspectors = expecteds.Select(
            expected => new Action<ConversationEvent>(actual => {
                Assert.Equal(expected.MemberId, actual.MemberId);
                Assert.Equal(expected.Year, actual.Created.Year);
                // Thread Id should always be set. So if we didn't set up an expected one, just ignore it for now.
                if (expected.ConversationEvent.ThreadId is { } expectedThreadId)
                {
                    Assert.Equal(expectedThreadId, actual.ThreadId);
                }
            })).ToArray();

        Assert.Collection(actualEvents, inspectors);
    }
}

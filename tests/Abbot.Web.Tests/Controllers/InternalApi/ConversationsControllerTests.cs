using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Serious.Abbot.Api;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Collections;

public class
    ConversationsControllerTests : ControllerTestBase<ConversationsController, ConversationsControllerTests.TestData>
{
    protected override string ExpectedArea => InternalApiControllerBase.Area;

    public class TestData : CommonTestData
    {
        public Room Room { get; private set; } = null!;

        public IReadOnlyList<Conversation> Conversations { get; private set; } = null!;

        public ConversationListWithStats ExpectedResults { get; private set; } = null!;

        public Dictionary<ConversationState, int> ExpectedStateCounts { get; private set; } = null!;

        public DateTime TestDate { get; } = new(2022, 1, 1);

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            Room = await env.CreateRoomAsync(platformRoomId: "Croom");
            Conversations = new List<Conversation>
            {
                await env.CreateConversationAsync(Room),
                await env.CreateConversationAsync(Room),
                await env.CreateConversationAsync(Room)
            };

            ExpectedStateCounts = new Dictionary<ConversationState, int>()
            {
                {ConversationState.Waiting, 1},
                {ConversationState.Archived, 1},
                {ConversationState.Closed, 1}
            };

            ExpectedResults = new ConversationListWithStats(
                new PaginatedList<Conversation>(Conversations, 420, 4, 10),
                new ConversationStats(ExpectedStateCounts, 420));
        }

        public void AssertResult(ConversationListResponseModel model)
        {
            Assert.Equal(Conversations.Count, model.Conversations.Count);
            Assert.Equal(
                new[] { Member.Id, Member.Id, Member.Id },
                model.Conversations.Select(c => c.StartedBy.Id).ToArray());

            Assert.Equal(
                new[] { Conversations[0].Title, Conversations[1].Title, Conversations[2].Title },
                model.Conversations.Select(c => c.Title.Text).ToArray());

            Assert.Equal(
                new[]
                {
                    "https://testorg.example.com/archives/Croom/p11110005",
                    "https://testorg.example.com/archives/Croom/p11110006",
                    "https://testorg.example.com/archives/Croom/p11110007"
                },
                model.Conversations.Select(c => c.FirstMessageUrl?.ToString()).ToArray());

            Assert.Equal(ExpectedResults.Stats, model.Stats);
            Assert.Equal(42, model.Pagination.TotalPages);
            Assert.Equal(4, model.Pagination.PageNumber);
        }
    }

    public class TheGetListAsyncMethod : ConversationsControllerTests
    {
        [Fact]
        public async Task Returns404IfRoomDoesNotExist()
        {
            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetListAsync(room: NonExistent.PlatformRoomId));

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal($"Room '{NonExistent.PlatformRoomId}' not found, or is not part of this organization.",
                problem.Detail);
        }

        [Fact]
        public async Task Returns400IfBothRoomAndRoleSpecified()
        {
            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetListAsync(room: NonExistent.PlatformRoomId, role: new[] { (RoomRole)0 }));

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
            Assert.Equal("Cannot specify both 'room' and 'role'", problem.Detail);
        }

        [Fact]
        public async Task AppliesStateFilterIfSpecified()
        {
            await RunGetListTestAsync(
                (_, c) => c.GetListAsync(state: ConversationStateFilter.Overdue, page: 4),
                e => new ConversationQuery(e.TestData.Organization.Id)
                                     .WithState(ConversationStateFilter.Overdue));
        }

        [Fact]
        public async Task AppliesRoomFilterIfSpecified()
        {
            await RunGetListTestAsync(
                (e, c) => c.GetListAsync(room: e.TestData.Room.PlatformRoomId, page: 4),
                e => new ConversationQuery(e.TestData.Organization.Id)
                                     .InRooms(Env.TestData.Room.Id));
        }

        [Fact]
        public async Task AppliesRoomAndStateFiltersIfSpecified()
        {
            await RunGetListTestAsync(
                (e, c) => c.GetListAsync(room: e.TestData.Room.PlatformRoomId,
                    state: ConversationStateFilter.Responded,
                    page: 4),
                e => new ConversationQuery(e.TestData.Organization.Id)
                    .InRooms(Env.TestData.Room.Id)
                    .WithState(ConversationStateFilter.Responded));
        }

        [Fact]
        public async Task AppliesRoleFilterIfSpecified()
        {
            await RunGetListTestAsync((_, c) => c.GetListAsync(role: new[] { RoomRole.FirstResponder, RoomRole.EscalationResponder }, page: 4),
                e => new ConversationQuery(e.TestData.Organization.Id)
                    .InRoomsWhereAssigned(Env.TestData.Member.Id, RoomRole.FirstResponder, RoomRole.EscalationResponder));
        }

        [Fact]
        public async Task AppliesRoleAndStateFiltersIfSpecified()
        {
            await RunGetListTestAsync(
                (_, c) => c.GetListAsync(role: new[] { RoomRole.FirstResponder, RoomRole.EscalationResponder },
                    state: ConversationStateFilter.Archived,
                    page: 4),
                e => new ConversationQuery(e.TestData.Organization.Id)
                    .InRoomsWhereAssigned(e.TestData.Member.Id, RoomRole.FirstResponder, RoomRole.EscalationResponder)
                    .WithState(ConversationStateFilter.Archived));
        }

        async Task RunGetListTestAsync(
            Func<TestEnvironmentWithData<TestData>, ConversationsController, Task<IActionResult>> action,
            Func<TestEnvironmentWithData<TestData>, ConversationQuery> expectedQueryFactory)
        {
            Env.Conversations.FakeQueryResults = Env.TestData.ExpectedResults;

            var expectedQuery = expectedQueryFactory(Env);
            var (_, result) = await InvokeControllerAsync(c => action(Env, c));
            var okResult = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsType<ConversationListResponseModel>(okResult.Value);
            Env.TestData.AssertResult(model);

            var call = Assert.Single(Env.Conversations.QueryCalls);
            Assert.Equal(expectedQuery, call.Query);
            Assert.Equal(4, call.PageNumber);
            Assert.Equal(WebConstants.LongPageSize, call.PageSize);
        }
    }

    public class TheGetQueueAsyncMethod : ConversationsControllerTests
    {
        [Fact]
        public async Task FetchesConversationQueueForUser()
        {
            var expectedQuery = ConversationQuery.QueueFor(Env.TestData.Member);
            Env.Conversations.FakeQueryResults = Env.TestData.ExpectedResults;

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetQueueAsync(4));

            var okResult = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsType<ConversationListResponseModel>(okResult.Value);
            Env.TestData.AssertResult(model);

            var call = Assert.Single(Env.Conversations.QueryCalls);
            Assert.Equal(expectedQuery, call.Query);
            Assert.Equal(4, call.PageNumber);
            Assert.Equal(WebConstants.LongPageSize, call.PageSize);
        }
    }

    public class TheGetTrendsAsyncMethod : ConversationsControllerTests
    {
        static readonly LocalDate StartDate = new(2022, 1, 1);

        [Theory]
        [InlineData(null, null, "America/Los_Angeles")]
        [InlineData("America/New_York", null, "America/New_York")]
        [InlineData("America/New_York", "Australia/Sydney", "Australia/Sydney")]
        public async Task FetchesRollUpUsingPacificTimeIfUserHasNoTimeZone(
            string userTimeZone,
            string specifiedTimeZone,
            string invocationTimeZone)
        {
            Env.TestData.Member.TimeZoneId = userTimeZone;
            await Env.Db.SaveChangesAsync();

            var rollUps = new List<ConversationTrendsRollup>()
            {
                new(
                    StartDate.AtMidnight().ToDateTimeUnspecified(),
                    StartDate.At(LocalTime.MaxValue).ToDateTimeUnspecified(),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(1),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(2),
                    23,
                    3),
                new(
                    StartDate.PlusDays(1).AtMidnight().ToDateTimeUnspecified(),
                    StartDate.PlusDays(1).At(LocalTime.MaxValue).ToDateTimeUnspecified(),
                    TimeSpan.FromMinutes(42),
                    TimeSpan.FromHours(4),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(5),
                    52,
                    6),
                new(
                    StartDate.PlusDays(2).AtMidnight().ToDateTimeUnspecified(),
                    StartDate.PlusDays(2).At(LocalTime.MaxValue).ToDateTimeUnspecified(),
                    TimeSpan.FromMinutes(23),
                    TimeSpan.FromHours(7),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(8),
                    99,
                    9),
            };

            var timezone = DateTimeZoneProviders.Tzdb[invocationTimeZone];
            var datePeriodSelector = new DatePeriodSelector(7, Env.Clock, timezone);
            var roomSelector = new AssignedRoomsSelector(Env.TestData.Member.Id, RoomRole.FirstResponder);
            Env.Conversations.FakeDailyRollups = rollUps;

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetTrendsAsync(DateRangeOption.Week, specifiedTimeZone));

            var okResult = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsType<TrendsResponseModel>(okResult.Value);
            Assert.Equal(invocationTimeZone, model.TimeZone);
            Assert.Equal(14400, model.Summary.AverageTimeToResponse);
            Assert.Equal(18000, model.Summary.AverageTimeToClose);
            Assert.Equal(18, model.Summary.NewConversations);
            Assert.Collection(model.Data,
                d1 => {
                    Assert.Equal(StartDate.ToDateTimeUnspecified(), d1.Date);
                    Assert.Equal(TimeSpan.FromHours(1).TotalSeconds, d1.AverageTimeToResponse);
                    Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, d1.AverageTimeToFirstResponseDuringCoverage);
                    Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, d1.AverageTimeToResponseDuringCoverage);
                    Assert.Equal(TimeSpan.FromHours(2).TotalSeconds, d1.AverageTimeToClose);
                    Assert.Equal(3, d1.NewConversations);
                },
                d1 => {
                    Assert.Equal(StartDate.PlusDays(1).ToDateTimeUnspecified(), d1.Date);
                    Assert.Equal(TimeSpan.FromHours(4).TotalSeconds, d1.AverageTimeToResponse);
                    Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, d1.AverageTimeToFirstResponseDuringCoverage);
                    Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, d1.AverageTimeToResponseDuringCoverage);
                    Assert.Equal(TimeSpan.FromHours(5).TotalSeconds, d1.AverageTimeToClose);
                    Assert.Equal(6, d1.NewConversations);
                },
                d1 => {
                    Assert.Equal(StartDate.PlusDays(2).ToDateTimeUnspecified(), d1.Date);
                    Assert.Equal(TimeSpan.FromHours(7).TotalSeconds, d1.AverageTimeToResponse);
                    Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, d1.AverageTimeToFirstResponseDuringCoverage);
                    Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, d1.AverageTimeToResponseDuringCoverage);
                    Assert.Equal(TimeSpan.FromHours(8).TotalSeconds, d1.AverageTimeToClose);
                    Assert.Equal(9, d1.NewConversations);
                });

            var call = Assert.Single(Env.Conversations.DailyRollupCalls);
            Assert.Equal(roomSelector, call.RoomSelector);
            Assert.Equal(datePeriodSelector, call.DatePeriodSelector);
            Assert.Same(Env.TestData.Organization, call.Organization);
        }
    }
}

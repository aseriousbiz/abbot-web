using System.Globalization;
using Abbot.Common.TestHelpers;
using NodaTime;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;
using Serious.TestHelpers;

public class InsightsRepositoryTests
{
    // ReSharper disable once MemberCanBePrivate.Global
    public class InsightsTestData : ConversationsTestData
    {
        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            FirstResponder = env.TestData.Member;
            await env.Roles.AddUserToRoleAsync(FirstResponder, Roles.Agent, env.TestData.Abbot);
            await base.SeedAsync(env);
            FirstConversation = await CreateConversationAsync(
                env,
                daysAgo: 120,
                firstResponder: FirstResponder);
        }

        public virtual async Task RunDefaultScriptAsync(TestEnvironmentWithData env)
        {
            await FirstConversation.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.ConversationSnoozed, 120), // Should be ignored.
                new(ConversationAction.FirstResponderResponds, 119),
                new(ConversationAction.CustomerResponds, 118),
                new(ConversationAction.ConversationOverdue, 116),
                new(ConversationAction.FirstResponderResponds, 23),
                new(ConversationAction.ConversationClosed, 3)
            });

            // Another conversation in the same room with the same first responder.
            var convo2 = await CreateConversationAsync(
                env,
                daysAgo: 456,
                room: FirstConversation.Room,
                firstResponder: FirstResponder);

            await convo2.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.FirstResponderResponds, 367),
                new(ConversationAction.CustomerResponds, 364),
                new(ConversationAction.ConversationSnoozed, 100),
                new(ConversationAction.ConversationAwakened, 99),
                new(ConversationAction.FirstResponderResponds, 5)
            });

            // Another conversation in the same room with the same first responder, but no responses.
            await CreateConversationAsync(
                env,
                daysAgo: 120,
                room: FirstConversation.Room,
                firstResponder: FirstResponder);

            await CreateConversationAsync(
                env,
                daysAgo: 28,
                room: FirstConversation.Room,
                firstResponder: FirstResponder);

            // Another conversation in a new room with a different first responder (SecondResponder).
            var convo5 = await CreateConversationAsync(env, daysAgo: 28);
            SecondResponder = convo5.FirstResponder;
            await convo5.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.FirstResponderResponds, 24),
                new(ConversationAction.CustomerResponds, 6),
                new(ConversationAction.FirstResponderResponds, 5),
                new(ConversationAction.ConversationClosed, 3),
            });

            // A new conversation with no responses.
            await CreateConversationAsync(env, daysAgo: 6, firstResponder: SecondResponder);
        }

        public Member FirstResponder { get; private set; } = null!;

        public Member SecondResponder { get; private set; } = null!;

        public ConversationEnvironment FirstConversation { get; private set; } = null!;
    }

    public class TheGetStatsAsyncMethod
    {
        [Theory]
        [InlineData(7, 2, 1, 5, 0)]
        [InlineData(30, 3, 3, 6, 1)]
        [InlineData(365, 3, 5, 6, 1)]
        public async Task ForOrganizationReturnsStatsInTimePeriod(
            int startDaysAgo,
            int expectedRespondedCount,
            int expectedOpenedConversationsCount,
            int expectedNeededAttentionCount,
            int expectedOverdueCount)
        {
            // We're gonna create our own test data for this test because the common test data doesn't really do SLOs.
            var env = TestEnvironment.Create<InsightsTestData>();
            await env.TestData.RunDefaultScriptAsync(env);
            var organization = env.TestData.Organization;
            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                organization,
                RoomSelector.AllRooms,
                new DateRangeSelector(nowUtc.AddDays(-1 * startDaysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedRespondedCount, results.RespondedCount);
            Assert.Equal(expectedOpenedConversationsCount, results.OpenedCount);
            Assert.Equal(expectedNeededAttentionCount, results.NeededAttentionCount);
        }

        [Theory]
        [InlineData(7, 1, 0, 3)]
        [InlineData(30, 2, 1, 4)]
        [InlineData(365, 2, 3, 4)]
        public async Task ForFirstResponderReturnsStatsInTimePeriodForThatResponder(
            int startDaysAgo,
            int expectedRespondedCount,
            int expectedOpenedConversationsCount,
            int expectedNeededAttentionCount)
        {
            // We're gonna create our own test data for this test because the common test data doesn't really do SLOs.
            var env = TestEnvironment.Create<InsightsTestData>();
            await env.TestData.RunDefaultScriptAsync(env);
            var firstResponder = env.TestData.FirstResponder;
            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                env.TestData.Organization,
                new AssignedRoomsSelector(firstResponder.Id, RoomRole.FirstResponder),
                new DateRangeSelector(nowUtc.AddDays(-1 * startDaysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedRespondedCount, results.RespondedCount);
            Assert.Equal(expectedOpenedConversationsCount, results.OpenedCount);
            Assert.Equal(expectedNeededAttentionCount, results.NeededAttentionCount);
        }

        [Theory]
        [InlineData(7, 1, 1, 2)]
        [InlineData(30, 1, 2, 2)]
        [InlineData(365, 1, 2, 2)]
        public async Task ForSecondFirstResponderReturnsStatsInTimePeriodForThatResponder(
            int startDaysAgo,
            int expectedRespondedCount,
            int expectedOpenedConversationsCount,
            int expectedNeededAttentionCount)
        {
            // We're gonna create our own test data for this test because the common test data doesn't really do SLOs.
            var env = TestEnvironment.Create<InsightsTestData>();
            await env.TestData.RunDefaultScriptAsync(env);
            var secondResponder = env.TestData.SecondResponder;
            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                env.TestData.Organization,
                new AssignedRoomsSelector(secondResponder.Id, RoomRole.FirstResponder),
                new DateRangeSelector(nowUtc.AddDays(-1 * startDaysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedRespondedCount, results.RespondedCount);
            Assert.Equal(expectedOpenedConversationsCount, results.OpenedCount);
            Assert.Equal(expectedNeededAttentionCount, results.NeededAttentionCount);
        }

        [Theory]
        [InlineData(7, 0)] // It was responded to more than 7 days ago.
        [InlineData(30, 0)] // It was responded to more than 30 days ago.
        [InlineData(365, 1)] // It was in a New state in the past 365 days.
        public async Task CountsNeededAttentionStateChangesCorrectlyForTimePeriod(int daysAgo, int expectedCount)
        {
            var env = TestEnvironment.Create<InsightsTestData>();
            await env.TestData.FirstConversation.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.FirstResponderResponds, 35)
            });

            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                new DateRangeSelector(nowUtc.AddDays(-1 * daysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedCount, results.NeededAttentionCount);
        }

        [Theory]
        [InlineData(7, 1)] // It was in the New state in the past 7 days.
        [InlineData(30, 1)] // It was in the New state in the past 30 days.
        [InlineData(365, 1)] // It was in the New state in the past 365 days.
        public async Task ForNeededAttentionCountCountsCreatedConversationsWithNoStateChanges(int daysAgo,
            int expectedCount)
        {
            var env = TestEnvironment.Create<InsightsTestData>();
            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                new DateRangeSelector(nowUtc.AddDays(-1 * daysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedCount, results.NeededAttentionCount);
        }

        [Theory]
        [InlineData(7, 1)] // An existing conversation was overdue with no state changes in the past 7 days.
        [InlineData(30, 2)] // A conversation moved out of the Overdue state (and one moved in) in the past 30 days.
        [InlineData(365, 2)] // Two conversations were moved into the Overdue state in the past 365 days.
        public async Task ForWentOverdueCountCountsConversationsThatAreAlreadyOverdue(int daysAgo, int expectedCount)
        {
            var env = TestEnvironment.Create<InsightsTestData>();
            var firstConversation = await env.TestData.CreateConversationAsync(env, 366);
            await firstConversation.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.ConversationOverdue, 360), new(ConversationAction.ConversationClosed, 28)
            });

            var anotherConversation = await env.TestData.CreateConversationAsync(env, 366);
            await anotherConversation.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.ConversationOverdue,
                    8) // Went overdue 8 days ago. No state change in the past 7, but was already overdue.
            });

            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                new DateRangeSelector(nowUtc.AddDays(-1 * daysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedCount, results.WentOverdueCount);
        }

        [Theory]
        [InlineData(7, 0)] // It was closed more than 7 days ago.
        [InlineData(30, 0)] // It was closed more than 30 days ago.
        [InlineData(365, 1)] // It was opened in the past 365.
        public async Task DoesNotCountClosedConversation(int daysAgo, int expectedCount)
        {
            var env = TestEnvironment.Create<InsightsTestData>();
            await env.TestData.FirstConversation.RunScriptAsync(new ConversationStep[]
            {
                new(ConversationAction.ConversationOverdue, 34), new(ConversationAction.ConversationClosed, 33)
            });

            var nowUtc = ConversationsTestData.NowUtc;
            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetSummaryStatsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                new DateRangeSelector(nowUtc.AddDays(-1 * daysAgo), nowUtc),
                TagSelector.Create(null),
                default);

            Assert.Equal(expectedCount, results.NeededAttentionCount);
            Assert.Equal(expectedCount, results.WentOverdueCount);
        }
    }

    public class TheGetConversationVolumeRollupsAsyncMethod
    {
        [Theory]
        [InlineData(null, new[] { 3, 1, 1, 0, 1, 2, 1 })]
        [InlineData("tag1", new[] { 3, 1, 1, 0, 1, 2, 1 })]
        [InlineData("tag99", new[] { 0, 0, 0, 0, 0, 0, 0 })]
        [InlineData("tag3", new[] { 2, 0, 1, 0, 0, 1, 1 })]
        [InlineData("tag4", new[] { 1, 1, 0, 0, 1, 1, 0 })]
        public async Task CountsNewConversationsByDay(string? tagFilter, int[] expectedCounts)
        {
            var env = TestEnvironment.Create<InsightsTestData>();
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");

            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var utcDates = new[]
            {
                "Apr 13 06:59", // Apr 12 11:59 PDT - OUT OF RANGE
                "Apr 13 07:00", // Apr 13 00:00 PDT
                "Apr 13 08:00", // Apr 13 01:00 PDT
                "Apr 14 06:59", // Apr 13 23:59 PDT
                "Apr 14 07:01", // Apr 14 00:01 PDT
                "Apr 15 07:00", // Apr 15 00:00 PDT
                // SKIPPED: Apr 16
                "Apr 17 07:00", // Apr 17 00:00 PDT
                "Apr 18 07:00", // Apr 18 00:00 PDT
                "Apr 18 07:00", // Apr 18 00:00 PDT
                "Apr 19 07:00", // Apr 19 00:00 PDT
                "Apr 20 07:00" // Apr 20 00:00 AM PDT - OUT OF RANGE
            };

            int modulo = 0;
            foreach (var createdDate in utcDates)
            {
                var secondTag = modulo++ % 2 == 1
                    ? "tag3"
                    : "tag4";
                var createdDateUtc = Dates.ParseUtc($"2022, {createdDate}");
                await env.TestData.CreateConversationAsync(env, createdDateUtc, tags: new[] { "tag1", secondTag });
            }

            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetConversationVolumeRollupsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                new DatePeriodSelector(7, clock, timeZone),
                TagSelector.Create(tagFilter),
                default);

            var expectedDays = new[] { "Apr 13", "Apr 14", "Apr 15", "Apr 16", "Apr 17", "Apr 18", "Apr 19" };
            Assert.Equal(expectedDays, results.Select(r => r.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray());
            Assert.Equal(expectedCounts, results.Select(r => r.New.GetValueOrDefault()).ToArray());
        }

        [Theory]
        [InlineData("Apr 12, 2022 07:00", null, new[] { 1, 1, 0, 1, 1, 1, 0 })] // Before the date range
        [InlineData("Apr 13, 2022 07:01", null, new[] { 1, 1, 0, 1, 1, 1, 0 })] // At the beginning of the date range
        [InlineData("Apr 12, 2022 07:00", "tag1", new[] { 1, 1, 0, 1, 1, 1, 0 })]
        [InlineData("Apr 12, 2022 07:00", "tag2", new[] { 0, 0, 0, 0, 0, 0, 0 })]
        public async Task CountsOpenConversationAsItOpensAndClosesOverThePeriod(
            string startingDateUtc,
            string? tagFilter,
            int[] expectedCounts)
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var organization = env.TestData.Organization;
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");

            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var datePeriodSelector = new DatePeriodSelector(7, clock, timeZone);

            // Created before or on the first day of the period.
            var created = Dates.ParseUtc(startingDateUtc);
            var conversation = await env.TestData.CreateConversationAsync(env, created);
            var tags = await env.Tags.EnsureTagsAsync(
                new[] { "tag1", "tag2" },
                null,
                env.TestData.Abbot,
                env.TestData.Organization);
            await env.Tags.TagConversationAsync(conversation.Conversation, new[] { tags[0].Id }, env.TestData.Abbot.User);

            // Closed on Apr 14, reopens during the day, but ends closed. So should be 0 on Apr 15.
            await conversation.Close("Apr 14 07:01");
            await conversation.CustomerResponds("Apr 14 08:01");
            await conversation.Close("Apr 14 09:01");

            // Starts closed on the 16: Reopens, closes, then ends day reopened
            await conversation.CustomerResponds("Apr 16 07:01");
            await conversation.Close("Apr 16 09:01");
            await conversation.CustomerResponds("Apr 16 10:02");

            // Closes, but reopens on the 17th.
            await conversation.Close("Apr 17 07:01");
            await conversation.CustomerResponds("Apr 17 07:02");

            // Finally closes for good on the 18th
            await conversation.Close("Apr 18 07:01");

            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetConversationVolumeRollupsAsync(
                organization,
                RoomSelector.AllRooms,
                datePeriodSelector,
                TagSelector.Create(tagFilter),
                default);

            // Expect local time to be Apr 13 - Apr 19
            var expectedDays = Enumerable.Range(13, 7).Select(day => $"Apr {day}").ToArray();
            var days = results.Select(r => r.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(expectedDays, days);
            Assert.Equal(expectedCounts, results.Select(r => r.Open.GetValueOrDefault()).ToArray());
        }

        [Theory]
        [InlineData(null, new[] { 6, 7, 5, 4, 3, 3, 3 })]
        [InlineData("tag0", new[] { 6, 7, 5, 4, 3, 3, 3 })]
        [InlineData("tag1", new[] { 3, 3, 3, 2, 1, 2, 3 })]
        [InlineData("tag2", new[] { 3, 4, 2, 2, 2, 1, 0 })]
        [InlineData("tag3", new[] { 0, 0, 0, 0, 0, 0, 0 })]
        public async Task CountsOpenConversationsByDay(string? tagFilter, int[] expectedCounts)
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");
            var tags = await env.Tags.EnsureTagsAsync(
                new[] { "tag0", "tag1", "tag2", "tag3" },
                null,
                env.TestData.Abbot,
                env.TestData.Organization);
            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var datePeriodSelector = new DatePeriodSelector(7, clock, timeZone);
            var fixtures = new[]
            {
                //  Created UTC, Closed UTC    )  //        Created PDT - Closes PDT
                ("Apr 11 07:00", "Apr 13 06:59"), //                                 - Closed before the range - Ignored.
                ("Apr 12 00:00", "Apr 17 07:00"), // Id: 1 - Apr 12 PDT - Apr 17 PDT
                ("Apr 12 07:00", null /*     */), // Id: 2 - Apr 12 PDT - (Never Closed)
                ("Apr 13 07:00", "Apr 14 09:00"), // Id: 3 - Apr 13 PDT - Apr 14 PDT
                ("Apr 13 08:00", "Apr 14 10:00"), // Id: 4 - Apr 13 PDT - Apr 14 PDT
                ("Apr 14 06:00", "Apr 15 06:00"), // Id: 5 - Apr 13 PDT - Apr 14 PDT
                ("Apr 14 06:30", "Apr 15 09:00"), // Id: 6 - Apr 13 PDT - Apr 15 PDT - Open: (1, 2, 3, 4, 5, 6),    Closed: ()      Count: 6
                ("Apr 14 07:30", "Apr 16 09:30"), // Id: 7 - Apr 14 PDT - Apr 16 PDT - Open: (1, 2, 3, 4, 5, 6, 7), Closed: (3,4,5) Count: 7
                ("Apr 15 12:00", "Apr 16 12:00"), // Id: 8 - Apr 15 PDT - Apr 16 PDT - Open: (1, 2, 6, 7, 8),       Closed: (6)     Count: 5
                //Apr 16                                                             - Open: (1, 2, 7, 8),          Closed: (7, 8)  Count: 4
                ("Apr 17 09:00", "Apr 17 10:00"), // Id: 9 - Apr 17 PDT - Apr 17 PDT - Open: (1, 2, 9),             Closed: (1, 9)  Count: 3
                ("Apr 18 09:00", "Apr 19 09:00"), // Id:10 - Apr 18 PDT - Apr 19 PDT -
                ("Apr 18 09:00", "Apr 18 09:01"), // Id:11 - Apr 18 PDT - Apr 18 PDT - Open: (2, 10, 11),           Closed: (11)    Count: 3
                ("Apr 19 09:00", null /*     */) //  Id:12 - Apr 19 PDT              - Open: (2, 10, 12),           Closed: (10)    Count: 3
            };

            var mod = 0;
            foreach (var (createdDateString, closedDateString) in fixtures)
            {
                var createdDateUtc = Dates.ParseUtc($"2022, {createdDateString}");
                var conversationFixture = await env.TestData.CreateConversationAsync(env, createdDateUtc);
                var conversation = conversationFixture.Conversation;
                var tagIds = mod++ % 2 is 0 ? new[] { tags[0].Id, tags[1].Id } : new[] { tags[0].Id, tags[2].Id };
                await env.Tags.TagConversationAsync(conversation, tagIds, env.TestData.Abbot.User);
                if (closedDateString is not null)
                {
                    await conversationFixture.Close(closedDateString);
                }
            }

            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetConversationVolumeRollupsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                datePeriodSelector,
                TagSelector.Create(tagFilter),
                default);

            // Expect local time to be Apr 13 - Apr 19
            var expectedDays = Enumerable.Range(13, 7).Select(day => $"Apr {day}").ToArray();
            var days = results.Select(r => r.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(expectedDays, days);
            Assert.Equal(expectedCounts, results.Select(r => r.Open.GetValueOrDefault()).ToArray());
        }

        [Theory]
        [InlineData(null, new[] { 1, 1, 0, 1, 1, 1, 0 })]
        [InlineData("tag0", new[] { 1, 1, 0, 1, 1, 1, 0 })]
        [InlineData("tag1", new[] { 0, 0, 0, 0, 0, 0, 0 })]
        public async Task CountsOverdueConversationAsItBecomesOverdueAndNotOverdue(
            string? tagFilter,
            int[] expectedCounts)
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var organization = env.TestData.Organization;
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");
            var tags = await env.Tags.EnsureTagsAsync(
                new[] { "tag0" },
                null,
                env.TestData.Abbot,
                env.TestData.Organization);
            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var datePeriodSelector = new DatePeriodSelector(7, clock, timeZone);

            // Created and becomes Overdue before the date range
            var created = Dates.ParseUtc("Apr 12, 2022 07:00"); // Prior to date range.
            var conversation = await env.TestData.CreateConversationAsync(env, created);
            await env.Tags.TagConversationAsync(conversation.Conversation, new[] { tags[0].Id }, env.TestData.Abbot.User);
            await conversation.Overdue(Dates.ParseUtc("Apr 12, 2022 08:00"));

            // Closed on the 14th.
            await conversation.Close(Dates.ParseUtc("Apr 14, 2022 07:01"));

            // Reopens and becomes overdue on the 16th
            await conversation.CustomerResponds(Dates.ParseUtc("Apr 16, 2022 07:01"));
            await conversation.Overdue(Dates.ParseUtc("Apr 16, 2022 08:00"));

            // Closes, but reopens and becomes overdue on the 17th.
            await conversation.Close(Dates.ParseUtc("Apr 17, 2022 07:01"));
            await conversation.CustomerResponds(Dates.ParseUtc("Apr 17, 2022 07:02"));
            await conversation.Overdue(Dates.ParseUtc("Apr 17, 2022 08:01"));

            // Finally closes for good on the 18th
            await conversation.Close(Dates.ParseUtc("Apr 18, 2022 07:01"));

            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetConversationVolumeRollupsAsync(
                organization,
                RoomSelector.AllRooms,
                datePeriodSelector,
                TagSelector.Create(tagFilter),
                default);

            // Expect local time to be Apr 13 - Apr 19
            var expectedDays = Enumerable.Range(13, 7).Select(day => $"Apr {day}").ToArray();
            var days = results.Select(r => r.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(expectedDays, days);
            Assert.Equal(expectedCounts, results.Select(r => r.Overdue.GetValueOrDefault()).ToArray());
        }

        [Theory]
        [InlineData(null, new[] { 1, 0, 2, 3, 2, 2, 1 })]
        [InlineData("tag0", new[] { 1, 0, 2, 3, 2, 2, 1 })]
        [InlineData("tag1", new[] { 1, 0, 1, 1, 1, 2, 1 })]
        [InlineData("tag2", new[] { 0, 0, 1, 2, 1, 0, 0 })]
        [InlineData("tag3", new[] { 0, 0, 0, 0, 0, 0, 0 })]
        public async Task CountsOverdueConversationsByDay(string? tagFilter, int[] expectedCounts)
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");
            var tags = await env.Tags.EnsureTagsAsync(
                new[] { "tag0", "tag1", "tag2" },
                null,
                env.TestData.Abbot,
                env.TestData.Organization);
            var nowUtc = ConversationsTestData.NowUtc; // Apr 20, 2022 @ 12:00:00 AM UTC => Apr 19, 2022 @ 5:00 PM PDT
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var fixtures = new[]
            {
                // Created UTC , Overdue UTC   , Closed UTC
                ("Apr 12 08:00", "Apr 12 09:00", "Apr 13 09:00"), // Id: 1 - Apr 12 (Overdue before the range, Closes on Apr 13)

                ("Apr 13 08:00", null, null), // Id: 3 - Apr 13 (Never overdue)  - Overdue: (1),       Closed: (1)
                ("Apr 14 08:00", null, null), // Id: 4 - Apr 14                  - Overdue: ()
                ("Apr 15 08:00", "Apr 15 09:00", "Apr 17 09:00"), // Id: 5 - Apr 15 (Closes Apr 17)
                ("Apr 15 09:00", "Apr 15 10:00", null), // Id: 6 - Apr 15 (Never closes)
                ("Apr 15 09:05", "Apr 16 08:00", "Apr 16 09:00"), // Id: 7 - Apr 15 (Overdue Apr 16) - Overdue: (5, 6)
                //       - Apr 16                  - Overdue: (5, 6, 7), Closed: (7)
                ("Apr 17 09:05", "Apr 18 09:00", "Apr 18 10:00"), // Id: 8 - Apr 17                  - Overdue: (5, 6),    Closed: (5)
                ("Apr 18 07:01", null, null), // Id: 9 - Apr 18                  - Overdue: (6, 8),    Closed: (8)
                ("Apr 19 07:00", null, null) // Id:10 - Apr 19                  - Overdue: (6)
            };

            int mod = 0;
            foreach (var (createdDateUtc, dateOverdueUtc, closeDateUtc) in fixtures)
            {
                var conversationFixture = await env.TestData.CreateConversationAsync(env, Dates.ParseUtc($"2022, {createdDateUtc}"));
                var tagIds = mod++ % 2 is 0 ? new[] { tags[0].Id, tags[1].Id } : new[] { tags[0].Id, tags[2].Id };
                await env.Tags.TagConversationAsync(conversationFixture.Conversation, tagIds, env.TestData.Abbot.User);

                if (dateOverdueUtc is not null)
                {
                    // Set the state to Overdue.
                    await conversationFixture.Overdue(dateOverdueUtc);
                }

                if (closeDateUtc is not null)
                {
                    await conversationFixture.Close(closeDateUtc);
                }
            }

            var repository = env.Activate<InsightsRepository>();

            var results = await repository.GetConversationVolumeRollupsAsync(
                env.TestData.Organization,
                RoomSelector.AllRooms,
                new DatePeriodSelector(7, clock, timeZone),
                TagSelector.Create(tagFilter),
                default);

            // Expect local time to be Apr 13 - Apr 19
            var expectedDays = Enumerable.Range(13, 7).Select(day => $"Apr {day}").ToArray();
            var days = results.Select(r => r.Date.ToString("MMM dd", CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(expectedDays, days);
            Assert.Equal(expectedCounts, results.Select(r => r.Overdue.GetValueOrDefault()).ToArray());
        }
    }

    public class TheGetConversationsOpenAtInstantCountAsyncMethod
    {
        [Theory]
        [InlineData(null, 3)]
        [InlineData("tag0", 3)]
        [InlineData("tag1", 2)]
        [InlineData("tag2", 1)]
        public async Task ReturnsCorrectCount(string? tagFilter, int expectedOpenCount)
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var tags = await env.Tags.EnsureTagsAsync(
                new[] { "tag0", "tag1", "tag2" },
                null,
                env.TestData.Abbot,
                env.TestData.Organization);
            var organization = env.TestData.Organization;
            var repository = env.Activate<InsightsRepository>();
            var nowUtc = Dates.ParseUtc("Apr 20, 2020 10:00");
            var (initialOpenCount, _) = await repository.GetOpenAndOverdueCountsBeforeDateAsync(
                organization,
                RoomSelector.AllRooms,
                TagSelector.Create(null),
                nowUtc);

            Assert.Equal(0, initialOpenCount);
            await env.TestData.CreateConversationAsync(
                env,
                nowUtc.AddSeconds(1),
                title: "Opened After the Instant: Ignore");

            var stillOpen = await env.TestData.CreateConversationAsync(
                env,
                nowUtc.AddDays(-1),
                title: "Opened Early, Never Closed: Count it");
            await env.Tags.TagConversationAsync(stillOpen.Conversation, new[] { tags[0].Id }, env.TestData.Abbot.User);

            var conversation = await env.TestData.CreateConversationAsync(
                env,
                nowUtc.AddDays(-1),
                title: "Closed before instant: Ignore");

            await conversation.Close(nowUtc.AddSeconds(-1));
            var openConversation = await env.TestData.CreateConversationAsync(
                env,
                nowUtc.AddDays(-1),
                title: "Closed after instant: Count it");
            await env.Tags.TagConversationAsync(
                openConversation.Conversation,
                new[] { tags[0].Id, tags[1].Id },
                env.TestData.Abbot.User);

            await openConversation.Close(nowUtc.AddSeconds(1));
            var reopenedBefore = await env.TestData.CreateConversationAsync(env,
                nowUtc.AddDays(-2),
                title: "Closed, then reopened before instant: Count it");
            await env.Tags.TagConversationAsync(
                reopenedBefore.Conversation,
                new[] { tags[0].Id, tags[1].Id, tags[2].Id },
                env.TestData.Abbot.User);

            await reopenedBefore.Close(nowUtc.AddDays(-1));
            await reopenedBefore.CustomerResponds(nowUtc.AddSeconds(-5));

            var (openCount, overdueCount) = await repository.GetOpenAndOverdueCountsBeforeDateAsync(
                organization,
                RoomSelector.AllRooms,
                TagSelector.Create(tagFilter),
                nowUtc);

            Assert.Equal(expectedOpenCount, openCount);
            Assert.Equal(0, overdueCount);
        }

        [Fact]
        public async Task ReturnsCorrectCountForDifferentDates()
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var organization = env.TestData.Organization;
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");

            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var datePeriodSelector = new DatePeriodSelector(7, clock, timeZone);

            // Created before the date range
            var created = Dates.ParseUtc("Apr 12, 2022 07:00"); // Prior to date range.
            var conversation = await env.TestData.CreateConversationAsync(env, created);
            Assert.True(created < datePeriodSelector.StartDateTimeUtc);
            Assert.Equal("Apr 13 07:00", datePeriodSelector.StartDateTimeUtc.ToString("MMM dd hh:mm"));

            // Closed on the 14th.
            await conversation.Close(Dates.ParseUtc("Apr 14, 2022 07:01"));

            var repository = env.Activate<InsightsRepository>();

            var (openCount, _) = await repository.GetOpenAndOverdueCountsBeforeDateAsync(
                organization,
                RoomSelector.AllRooms,
                TagSelector.Create(null),
                datePeriodSelector.StartDateTimeUtc);

            Assert.Equal(1, openCount);
        }
    }

    public class TheGetConversationsOverdueAtInstantCountAsyncMethod
    {
        [Fact]
        public async Task ReturnsCorrectCount()
        {
            var env = TestEnvironment.Create<EmptyConversationTestData>();
            var organization = env.TestData.Organization;
            var repository = env.Activate<InsightsRepository>();
            var nowUtc = Dates.ParseUtc("Apr 20, 2020 10:00");
            var (_, initialOverdue) = await repository.GetOpenAndOverdueCountsBeforeDateAsync(
                organization,
                RoomSelector.AllRooms,
                TagSelector.Create(null),
                nowUtc);

            Assert.Equal(0, initialOverdue);
            // Create a conversation that will be overdue before the instant.
            var overdue = await env.TestData.CreateConversationAsync(env, nowUtc.AddDays(-1));
            await overdue.Overdue(nowUtc.AddMinutes(-1));
            // Create a conversation that will be overdue after the instant and should be ignored.
            var overdueAfter = await env.TestData.CreateConversationAsync(env, nowUtc.AddDays(-1));
            await overdueAfter.Overdue(nowUtc.AddMinutes(1));
            // Create a conversation that is overdue before the instant and closed just before the instant.
            var conversation = await env.TestData.CreateConversationAsync(env, nowUtc.AddDays(-1));
            await conversation.Overdue(nowUtc.AddMinutes(-10));
            await conversation.Close(nowUtc.AddSeconds(-1));
            // Create another conversation that will be overdue at the instant and closed soon after.
            var overdueClosedAfter = await env.TestData.CreateConversationAsync(env, nowUtc.AddDays(-1));
            await overdueClosedAfter.Overdue(nowUtc.AddMinutes(-10));
            await overdueClosedAfter.Close(nowUtc.AddSeconds(1));

            var (openCount, overdueCount) = await repository.GetOpenAndOverdueCountsBeforeDateAsync(
                organization,
                RoomSelector.AllRooms,
                TagSelector.Create(null),
                nowUtc);

            Assert.Equal(2, overdueCount);
            Assert.Equal(3, openCount);
        }
    }

    public class TheWhereReopenedDuringPeriodMethod
    {
        [Theory]
        [InlineData(ConversationState.Unknown, new ConversationState[0], false)]
        [InlineData(ConversationState.Archived, new[] { ConversationState.Waiting, ConversationState.Archived, ConversationState.NeedsResponse }, true)]
        [InlineData(ConversationState.Closed, new[] { ConversationState.Waiting, ConversationState.Archived }, true)]
        [InlineData(ConversationState.NeedsResponse, new[] { ConversationState.Closed, ConversationState.NeedsResponse }, false)]
        [InlineData(ConversationState.Waiting, new[] { ConversationState.Archived, ConversationState.NeedsResponse }, false)]
        [InlineData(ConversationState.Archived, new[] { ConversationState.NeedsResponse, ConversationState.Archived, ConversationState.NeedsResponse }, true)]
        public void ReturnsTrueWhenConversationBecomesOpenAndNotAlreadyOpened(
            ConversationState initialState,
            IReadOnlyList<ConversationState> stateChanges,
            bool expected)
        {
            var oldState = initialState;
            var stateChangeEvents = stateChanges
                .Select(newState => {
                    var stateChangeEvent = new StateChangedEvent
                    {
                        Id = 1,
                        OldState = oldState,
                        NewState = newState
                    };

                    oldState = newState;
                    return stateChangeEvent;
                });

            var result = InsightsRepository.WhereReopenedDuringPeriod(stateChangeEvents);

            Assert.Equal(expected, result);
        }
    }

    public class TheWhereNotAlreadyOverdueAndWentOverdueDuringPeriodMethod
    {
        [Theory]
        [InlineData(ConversationState.Overdue, new ConversationState[0], false)]
        [InlineData(ConversationState.Overdue, new[] { ConversationState.Waiting, ConversationState.Overdue, ConversationState.NeedsResponse }, false)]
        [InlineData(ConversationState.Overdue, new[] { ConversationState.Waiting, ConversationState.Overdue }, false)]
        [InlineData(ConversationState.New, new[] { ConversationState.Overdue }, true)]
        [InlineData(ConversationState.NeedsResponse, new[] { ConversationState.Overdue, ConversationState.NeedsResponse }, true)] // Was not overdue: became overdue, then not overdue, then became overdue again.
        [InlineData(ConversationState.NeedsResponse, new[] { ConversationState.Overdue, ConversationState.NeedsResponse, ConversationState.Overdue }, true)]
        [InlineData(ConversationState.Overdue, new[] { ConversationState.NeedsResponse, ConversationState.Overdue }, false)]
        public void ReturnsTrueWhenConversationBecomesOverdueAndNotAlreadyOverdue(
            ConversationState initialState,
            IReadOnlyList<ConversationState> stateChanges,
            bool expected)
        {
            var oldState = initialState;
            var stateChangeEvents = stateChanges
                .Select(newState => {
                    var stateChangeEvent = new StateChangedEvent
                    {
                        Id = 1,
                        OldState = oldState,
                        NewState = newState
                    };

                    oldState = newState;
                    return stateChangeEvent;
                });

            var result = InsightsRepository.WhereNotAlreadyOverdueAndWentOverdueDuringPeriod(stateChangeEvents);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReturnsFalseForNoEvents()
        {
            var stateChangeEvents = Enumerable.Empty<StateChangedEvent>();

            var result = InsightsRepository.WhereNotAlreadyOverdueAndWentOverdueDuringPeriod(stateChangeEvents);

            Assert.False(result);
        }

        [Fact]
        public void ReturnsFalseForNoOverdueEvents()
        {
            var stateChangeEvents = new StateChangedEvent[]
            {
                new()
                {
                    OldState = ConversationState.New,
                    NewState = ConversationState.Waiting
                },
                new()
                {
                    OldState = ConversationState.Waiting,
                    NewState = ConversationState.NeedsResponse
                }
            };

            var result = InsightsRepository.WhereNotAlreadyOverdueAndWentOverdueDuringPeriod(stateChangeEvents);

            Assert.False(result);
        }
    }

    public class TheGetConversationsClosedDuringPeriodAtPeriodEndCountMethod
    {
        [Fact]
        public void CountsUniqueConversationsThatEndWithState()
        {
            var conversations = new[]
            {
                new
                {
                    Id = 0,
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.Unknown,
                            NewState = ConversationState.Unknown,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        }
                    }
                },
                new
                {
                    Id = 1,
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.Closed,
                            NewState = ConversationState.NeedsResponse,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        }
                    }
                },
                new
                {
                    Id = 2,
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.Closed,
                            NewState = ConversationState.NeedsResponse,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        }
                    }
                },
                new
                {
                    Id = 3,
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.Closed,
                            NewState = ConversationState.NeedsResponse,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        },
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:02")
                        }
                    }
                }
            };

            var stateChangeEvents = conversations
                .SelectMany(c => c.StateChanges.Select(e => new StateChangedEvent
                {
                    ConversationId = c.Id,
                    OldState = e.OldState,
                    NewState = e.NewState,
                }));

            var result = InsightsRepository.GetConversationsClosedDuringPeriodAtPeriodEndCount(stateChangeEvents);

            Assert.Equal(2, result);
        }
    }

    public class TheWhereOverdueInPeriodButEndedNotOverdueMethod
    {
        [Fact]
        public void ReturnsFalseWhenNoEvents()
        {
            var events = Enumerable.Empty<StateChangedEvent>();

            var result = InsightsRepository.WhereOverdueInPeriodButEndedNotOverdue(events);

            Assert.False(result);
        }

        [Fact]
        public void ReturnsFalseWhenNeverOverdue()
        {
            var events = new StateChangedEvent[]
            {
                new()
                {
                    OldState = ConversationState.NeedsResponse,
                    NewState = ConversationState.Closed,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                },
                new()
                {
                    OldState = ConversationState.Closed,
                    NewState = ConversationState.NeedsResponse,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                },
                new()
                {
                    OldState = ConversationState.NeedsResponse,
                    NewState = ConversationState.Closed,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:02")
                }
            };

            var result = InsightsRepository.WhereOverdueInPeriodButEndedNotOverdue(events);

            Assert.False(result);
        }

        [Fact]
        public void ReturnsTrueWhenBecameOverdueDuringPeriodButLeftPeriodNotOverdue()
        {
            var events = new StateChangedEvent[]
            {
                new()
                {
                    OldState = ConversationState.NeedsResponse,
                    NewState = ConversationState.Overdue,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                },
                new()
                {
                    OldState = ConversationState.Overdue,
                    NewState = ConversationState.NeedsResponse,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                },
                new()
                {
                    OldState = ConversationState.NeedsResponse,
                    NewState = ConversationState.Waiting,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                }
            };

            var result = InsightsRepository.WhereOverdueInPeriodButEndedNotOverdue(events);

            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueWhenEnteredOverdueLeftNotOverdue()
        {
            var events = new StateChangedEvent[]
            {
                new()
                {
                    OldState = ConversationState.Overdue,
                    NewState = ConversationState.Waiting,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                },
                new()
                {
                    OldState = ConversationState.Waiting,
                    NewState = ConversationState.NeedsResponse,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                }
            };

            var result = InsightsRepository.WhereOverdueInPeriodButEndedNotOverdue(events);

            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueWhenEnteredOverdueStoppedBeingOverdueBecameOverdueThenWasNotOverdueAtEnd()
        {
            var events = new StateChangedEvent[]
            {
                new()
                {
                    OldState = ConversationState.Overdue,
                    NewState = ConversationState.Waiting,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                },
                new()
                {
                    OldState = ConversationState.Waiting,
                    NewState = ConversationState.NeedsResponse,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                },
                new()
                {
                    OldState = ConversationState.NeedsResponse,
                    NewState = ConversationState.Overdue,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:02")
                },
                new()
                {
                    OldState = ConversationState.Overdue,
                    NewState = ConversationState.Closed,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:03")
                }
            };

            var result = InsightsRepository.WhereOverdueInPeriodButEndedNotOverdue(events);

            Assert.True(result);
        }
    }

    public class TheGetOverdueConversationsNoLongerOverdueCountMethod
    {
        [Fact]
        public void ReturnsCountOfOverdueConversationsNoLongerOverdue()
        {
            var conversations = new[]
            {
                new
                {
                    Id = 0, // COUNT IT (Was overdue, but no longer)!
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.Overdue,
                            NewState = ConversationState.Waiting,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.Waiting,
                            NewState = ConversationState.NeedsResponse,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        }
                    }
                },
                new
                {
                    Id = 1, // DO NOT COUNT IT (Overdue at the end)
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.Overdue,
                            NewState = ConversationState.Waiting,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Overdue,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        }
                    }
                },
                new
                {
                    Id = 2, // COUNT IT! (It became overdue during the period)
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Overdue,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.Overdue,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        }
                    }
                },
                new
                {
                    Id = 3, // DO NOT COUNT IT (Was never overdue)
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        },
                        new()
                        {
                            OldState = ConversationState.Closed,
                            NewState = ConversationState.NeedsResponse,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:01")
                        },
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Closed,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:02")
                        }
                    }
                },
                new
                {
                    Id = 4, // DO NOT COUNT IT Became overdue.
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.NeedsResponse,
                            NewState = ConversationState.Overdue,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        }
                    }
                },
                new
                {
                    Id = 5, // COUNT IT!
                    StateChanges = new StateChangedEvent[]
                    {
                        new()
                        {
                            OldState = ConversationState.Overdue,
                            NewState = ConversationState.NeedsResponse,
                            Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                        }
                    }
                }
            };

            var stateChangeEvents = conversations
                .SelectMany(c => c.StateChanges.Select(e => new StateChangedEvent
                {
                    ConversationId = c.Id,
                    OldState = e.OldState,
                    NewState = e.NewState,
                }));

            var result = InsightsRepository.GetOverdueConversationsNoLongerOverdueCount(stateChangeEvents);

            Assert.Equal(3, result);
        }

        [Fact]
        public void ReturnsZeroWhenNoConversationsTransitionedToState()
        {
            var stateChangeEvents = new StateChangedEvent[]
            {
                new()
                {
                    ConversationId = 0,
                    OldState = ConversationState.Unknown,
                    NewState = ConversationState.Unknown,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                },
                new()
                {
                    ConversationId = 1,
                    OldState = ConversationState.Closed,
                    NewState = ConversationState.NeedsResponse,
                    Created = Dates.ParseUtc("Apr 20, 2022 07:00")
                }
            };

            var result = InsightsRepository.GetOverdueConversationsNoLongerOverdueCount(stateChangeEvents);

            Assert.Equal(0, result);
        }
    }

    public class TheGetRoomFilterListMethod
    {
        [Fact]
        public async Task RetrievesManagedRoomsForTheOrganizationAndNotOtherOrganizations()
        {
            var env = TestEnvironment.Create();
            await env.CreateRoomAsync(name: "Room 1", managedConversationsEnabled: true);
            await env.CreateRoomAsync(name: "Room 2", managedConversationsEnabled: true);
            await env.CreateRoomAsync(name: "Room 3", managedConversationsEnabled: true, org: env.TestData.ForeignOrganization);
            await env.CreateRoomAsync(name: "Room 4", managedConversationsEnabled: false);
            var roomWasManaged = await env.CreateRoomAsync(name: "Room 5", managedConversationsEnabled: false);
            await env.CreateConversationAsync(roomWasManaged);
            var repository = env.Activate<InsightsRepository>();

            var rooms = await repository.GetRoomFilterList(env.TestData.Organization);

            Assert.Collection(rooms,
                r0 => Assert.Equal("Room 1", r0.Name),
                r1 => Assert.Equal("Room 2", r1.Name),
                r1 => Assert.Equal("Room 5", r1.Name));
        }
    }

    public class TheGetConversationVolumeByRoomAsyncMethod
    {
        [Fact]
        public async Task GetsConversationVolumePerRoom()
        {
            var env = TestEnvironment.Create();
            var clock = env.Clock;
            var organization = env.TestData.Organization;
            var fixtures = new (string RoomName, bool Enabled, bool SameOrg, int NumberOpen)[]
            {
                ("Room 1", true, true, 2),
                ("Room 2", true, true, 0),
                ("Room 3", true, true, 5),
                ("Room 4", true, false, 2), // Ignored because in another org.
                ("Room 5", false, true, 3)  // Ignored because not managed.
            };
            foreach (var (name, enabled, sameOrg, convoCount) in fixtures)
            {
                var org = sameOrg
                    ? env.TestData.Organization
                    : env.TestData.ForeignOrganization;
                var room = await env.CreateRoomAsync(name: name, managedConversationsEnabled: enabled, org: org);
                for (int i = 0; i < convoCount; i++)
                {
                    await env.CreateConversationAsync(room);
                }

                // Create a conversation before the date range. This should not be counted.
                await env.CreateConversationAsync(room, timestamp: clock.UtcNow.AddDays(-366));
            }
            var roomSelector = RoomSelector.AllRooms;
            var datePeriodSelector = new DatePeriodSelector(7, clock, DateTimeZone.Utc);
            var repository = env.Activate<InsightsRepository>();
            var result = await repository.GetConversationVolumeByRoomAsync(
                organization,
                roomSelector,
                datePeriodSelector,
                TagSelector.Create(null),
                default);

            Assert.Collection(result,
                r => Assert.Equal(("Room 3", 5), (r.Room.Name, r.OpenConversationCount)),
                r => Assert.Equal(("Room 1", 2), (r.Room.Name, r.OpenConversationCount)),
                r => Assert.Equal(("Room 2", 0), (r.Room.Name, r.OpenConversationCount)));

            var filteredResult = await repository.GetConversationVolumeByRoomAsync(
                organization,
                new SpecificRoomsSelector(result[0].Room.Id, result[2].Room.Id),
                datePeriodSelector,
                TagSelector.Create(null),
                default);

            Assert.Collection(filteredResult,
                r => Assert.Equal(("Room 3", 5), (r.Room.Name, r.OpenConversationCount)),
                r => Assert.Equal(("Room 2", 0), (r.Room.Name, r.OpenConversationCount)));
        }
    }

    public class TheGetConversationVolumeByResponderAsyncMethod
    {
        [Fact]
        public async Task GetsConversationVolumePerResponder()
        {
            var env = TestEnvironment.Create();
            var clock = env.Clock;
            var organization = env.TestData.Organization;
            var customer = env.TestData.ForeignMember;
            var guest = env.TestData.Guest;
            var responder1 = env.TestData.Member;
            var responder2 = await env.CreateMemberInAgentRoleAsync();
            var responder3 = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true, org: organization);
            // Count: responder1, responder 2
            var convo1 = await env.CreateConversationAsync(room, startedBy: customer);
            var message1 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo1,
                new MessagePostedEvent { MessageId = message1 },
                env.CreateConversationMessage(convo1, from: responder1, messageId: message1),
                false);
            var message2 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo1,
                new MessagePostedEvent { MessageId = message2 },
                env.CreateConversationMessage(convo1, from: responder1, messageId: message2),
                false);
            var message3 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo1,
                new MessagePostedEvent { MessageId = message3 },
                env.CreateConversationMessage(convo1, from: responder2, messageId: message3),
                false);
            var message4 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo1,
                new MessagePostedEvent { MessageId = message4 },
                env.CreateConversationMessage(convo1, from: responder1, messageId: message4),
                false);

            // Ignore
            var unmanagedRoom = await env.CreateRoomAsync(managedConversationsEnabled: false, org: organization);
            var ignoredConvo = await env.CreateConversationAsync(unmanagedRoom, startedBy: customer);
            var message5 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(ignoredConvo,
                new MessagePostedEvent { MessageId = message5 },
                env.CreateConversationMessage(ignoredConvo, from: responder1, messageId: message5),
                false);
            var message6 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(ignoredConvo,
                new MessagePostedEvent { MessageId = message6 },
                env.CreateConversationMessage(ignoredConvo, from: responder3, messageId: message6),
                false);

            // Ignore
            var foreignRoom = await env.CreateRoomAsync(managedConversationsEnabled: true,
                org: env.TestData.ForeignOrganization);
            var ignoredConvo2 = await env.CreateConversationAsync(foreignRoom, startedBy: customer);
            var message7 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(ignoredConvo2,
                new MessagePostedEvent { MessageId = message7 },
                env.CreateConversationMessage(ignoredConvo2, from: responder3, messageId: message7),
                false);

            var legitRoom = await env.CreateRoomAsync(managedConversationsEnabled: true, org: organization);
            // Count: responder1
            var convo2 = await env.CreateConversationAsync(legitRoom, startedBy: customer);
            var message8 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo2,
                new MessagePostedEvent { MessageId = message8 },
                env.CreateConversationMessage(convo2, from: responder1, messageId: message8),
                false);
            // Count: responder2
            // Also, we throw a 'guest' user convo in here to ensure guests are not counted as home users.
            var convo3 = await env.CreateConversationAsync(legitRoom, startedBy: guest);
            var message9 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo3,
                new MessagePostedEvent { MessageId = message9 },
                env.CreateConversationMessage(convo3, from: responder2, messageId: message9),
                false);
            // Count: responder2
            var convo4 = await env.CreateConversationAsync(legitRoom, startedBy: customer);
            var message10 = env.IdGenerator.GetSlackMessageId();
            await env.Conversations.UpdateForNewMessageAsync(convo4,
                new MessagePostedEvent { MessageId = message10 },
                env.CreateConversationMessage(convo4, from: responder2, messageId: message10),
                false);

            var repository = env.Activate<InsightsRepository>();

            var result = await repository.GetConversationVolumeByResponderAsync(
                organization,
                RoomSelector.AllRooms,
                new DatePeriodSelector(7, clock, DateTimeZone.Utc),
                TagSelector.Create(null),
                default);

            Assert.Collection(result.OrderBy(r => r.Member.Id),
                r => Assert.Equal((responder1.Id, 2), (r.Member.Id, r.OpenConversationCount)),
                r => Assert.Equal((responder2.Id, 3), (r.Member.Id, r.OpenConversationCount)));
        }
    }

    public class EmptyConversationTestData : ConversationsTestData
    {
    }

    public abstract class ConversationsTestData : CommonTestData
    {
        public static DateTime NowUtc => new(2022, 04, 20, 0, 0, 0, DateTimeKind.Utc);

        public Task<ConversationEnvironment> CreateConversationAsync(
            TestEnvironmentWithData env,
            int daysAgo,
            Member? firstResponder = null,
            Room? room = null) => CreateConversationAsync(env, TimeSpan.FromDays(daysAgo), firstResponder, room);

        public Task<ConversationEnvironment> CreateConversationAsync(
            TestEnvironmentWithData env,
            TimeSpan timeAgo, // Subtract from NowUtc for the created date.
            Member? firstResponder = null,
            Room? room = null) => CreateConversationAsync(env, NowUtc.Subtract(timeAgo), firstResponder, room);

        public async Task<ConversationEnvironment> CreateConversationAsync(
            TestEnvironmentWithData env,
            DateTime createdDateUtc,
            Member? firstResponder = null,
            Room? room = null,
            string? title = null,
            string[]? tags = null)
        {
            firstResponder ??= await env.CreateMemberInAgentRoleAsync();
            var customer = env.TestData.ForeignMember;
            if (room is null)
            {
                room = await env.CreateRoomAsync();
                room.TimeToRespond = new Threshold<TimeSpan>(
                    TimeSpan.FromDays(1),
                    TimeSpan.FromDays(2));
            }

            await env.Rooms.AssignMemberAsync(room, firstResponder, RoomRole.FirstResponder, env.TestData.Member);
            await env.Db.SaveChangesAsync();
            var conversation = await env.CreateConversationAsync(
                room,
                title ?? $"Convo {env.IdGenerator.GetId()}",
                createdDateUtc,
                startedBy: customer);

            if (tags is not null)
            {
                var tagEntities = await env.Tags.EnsureTagsAsync(
                    tags,
                    null,
                     env.TestData.Abbot,
                    env.TestData.Organization);
                await env.Tags.TagConversationAsync(conversation, tagEntities.Select(t => t.Id), env.TestData.Abbot.User);
            }

            return new ConversationEnvironment(env, firstResponder, customer, conversation, NowUtc);
        }
    }
}

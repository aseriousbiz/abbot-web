using System.Net;
using Abbot.Common.TestHelpers;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;
using Serious.TestHelpers;

public class MissingConversationsReporterTests
{
    public class TheEnsureTrackedConversationsAsyncMethod
    {
        [Fact]
        public async Task LogsMessagesCreatedAfterManagedConversationsEnabledThatShouldBeTrackedButIsNot()
        {
            var env = TestEnvironment.Create<TestDataWithMissingConversations>();
            env.StopwatchFactory.Elapsed = TimeSpan.FromMilliseconds(63);
            var organization = env.TestData.Organization;
            var roomOneMissing = env.TestData.RoomWithOneMissingConversation;
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(organization);

            var errorMessages = env.GetAllLogs<MissingConversationsReporter>(LogLevel.Error);
            var logged = Assert.Single(errorMessages);

            Assert.Equal($"""
Found 5 missing conversations for Funny Business (T008675309) in 63 milliseconds.
    channel: C0000008, Missed: 1642913779.429981, 1642913774.150735, 1642913769.708755, 1642913759.423639
    channel: {roomOneMissing.PlatformRoomId}, Missed: 1642913778.112116

""", logged.Message);
            Assert.NotNull(logged.State);
        }

        [Fact]
        public async Task IgnoresExceptionsAndMovesToNextRoom()
        {
            var env = TestEnvironment
                .Create<TestDataWithMissingConversationsWithException>();
            env.StopwatchFactory.Elapsed = TimeSpan.FromMilliseconds(42);
            var organization = env.TestData.Organization;
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(organization);

            var expectedResultsMessage = $"""
Found 5 missing conversations for Funny Business (T008675309) in 42 milliseconds.
    channel: C0000008, Missed: 1642913779.429981, 1642913774.150735, 1642913769.708755, 1642913759.423639
    channel: C0000010, Missed: 1642913778.112116

""";
            var errorMessages = env.GetAllLogs<MissingConversationsReporter>(LogLevel.Error);
            Assert.Collection(errorMessages,
                m => Assert.Equal("Exception calling API: C000000042 in org Funny Business (T008675309):\n{\"error\":\"Houston, we have a problem\"}", m.Message),
                m => Assert.Equal("Exception calling API: C000000099 in org Funny Business (T008675309)", m.Message),
                m =>
                    Assert.Equal(expectedResultsMessage, m.Message));
        }

        [Fact]
        public async Task ReportsProgressWhenCancelled()
        {
            var env = TestEnvironment.Create<TestDataWithOperationCancelledException>();
            env.StopwatchFactory.Elapsed = TimeSpan.FromMilliseconds(42);
            var organization = env.TestData.Organization;
            var job = env.Activate<MissingConversationsReporter>();

            await Assert.ThrowsAsync<OperationCanceledException>(() => job.LogUntrackedConversationsAsync(organization));

            var expectedResultsMessage = $"""
Missing Conversations Repair job cancelled after examining 2 of 4 rooms. Results so far:
Found 5 missing conversations for Funny Business (T008675309) in 42 milliseconds.
    channel: C0000008, Missed: 1642913779.429981, 1642913774.150735, 1642913769.708755, 1642913759.423639
    channel: C0000010, Missed: 1642913778.112116

""";
            var errorMessages = env.GetAllLogs<MissingConversationsReporter>(LogLevel.Error);
            var message = Assert.Single(errorMessages);
            Assert.Equal(expectedResultsMessage, message.Message);
        }

        [Fact]
        public async Task DoesNotLogErrorWhenEveryMessageAccountedFor()
        {
            var env = TestEnvironment.Create<TestDataNoMissing>();
            var organization = env.TestData.Organization;
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(organization);

            var errorMessages = env.GetAllLogs<MissingConversationsReporter>(LogLevel.Error);
            Assert.Empty(errorMessages);
        }

        [Fact]
        public async Task UpdatesLastMessageIdWithLastMessageIdReturnedFromTheApi()
        {
            var env = TestEnvironment.Create<TestDataNoMissing>();
            var room = env.TestData.RoomWithNoMissing;
            var lastVerifiedMessageId = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal("1642913722.918019", lastVerifiedMessageId);
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(env.TestData.Organization);

            var updatedVerifiedMessageId = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal("1643015291.611399", updatedVerifiedMessageId);
        }

        [Fact]
        public async Task SetsLastMessageIdToTimestampForCurrentDateIfApiCallFails()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var now = env.Clock.UtcNow;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var currentTimestamp = new SlackTimestamp(now).ToString();
            var apiToken = env.TestData.Organization.ApiToken!.Reveal();
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                apiToken,
                channel: room.PlatformRoomId,
                oldest: currentTimestamp,
                error: "Something Broke");
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(env.TestData.Organization);

            var updatedVerifiedMessageId = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal(currentTimestamp, updatedVerifiedMessageId);
        }

        [Fact]
        public async Task SetsLastMessageIdToTimestampForCurrentDateIfNotSetAndNoRecordsReturned()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var now = env.Clock.UtcNow;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var currentTimestamp = new SlackTimestamp(now).ToString();
            var apiToken = env.TestData.Organization.ApiToken!.Reveal();
            var initialMessages = Array.Empty<SlackMessage>();
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                apiToken,
                channel: room.PlatformRoomId,
                oldest: currentTimestamp,
                initialMessages);
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(env.TestData.Organization);

            var updatedVerifiedMessageId = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal(currentTimestamp, updatedVerifiedMessageId);
        }

        [Fact]
        public async Task DoesNotOverwriteLastMessageIdIfNoNewMessagesReturned()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var now = env.Clock.UtcNow;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var lastVerifiedMessageId = new SlackTimestamp(now.AddDays(-1)).ToString();
            await env.Settings.SetLastVerifiedMessageIdAsync(room, lastVerifiedMessageId, env.TestData.Abbot.User);

            await env.Db.SaveChangesAsync();
            var apiToken = env.TestData.Organization.ApiToken!.Reveal();
            var initialMessages = Array.Empty<SlackMessage>();
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                apiToken,
                channel: room.PlatformRoomId,
                oldest: lastVerifiedMessageId,
                initialMessages);
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(env.TestData.Organization);

            var updatedVerifiedMessageId = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal(lastVerifiedMessageId, updatedVerifiedMessageId);
        }

        [Fact]
        public async Task UpdatesLastMessageIdWithConstructedMessageIdOnFirstRunEvenIfApiDoesNotReturnResults()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var slackTimestamp = new SlackTimestamp(env.Clock.UtcNow).ToString();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var apiToken = env.TestData.Organization.ApiToken!.Reveal();
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                apiToken,
                room.PlatformRoomId,
                slackTimestamp,
                new List<SlackMessage>());
            var job = env.Activate<MissingConversationsReporter>();

            await job.LogUntrackedConversationsAsync(env.TestData.Organization);

            var updatedVerifiedMessageId = await env.Settings.GetLastVerifiedMessageIdAsync(room);
            Assert.Equal(slackTimestamp, updatedVerifiedMessageId);
        }

        [Fact]
        public async Task ThrowsOperationCancelledExceptionIfTokenCancellationRequested()
        {
            var env = TestEnvironment.Create<TestDataNoMissing>();
            var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var job = env.Activate<MissingConversationsReporter>();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => job.LogUntrackedConversationsAsync(env.TestData.Organization, tokenSource.Token));
        }
    }

    public class TestDataNoMissing : CommonTestData
    {
        protected string ApiToken { get; private set; } = null!;

        public Room RoomWithNoMissing { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);

            env.Clock.TravelTo(new DateTime(2022, 1, 23, 4, 56, 20));
            var organization = env.TestData.Organization;
            organization.Name = "Funny Business";
            organization.PlatformId = "T008675309";
            await env.Db.SaveChangesAsync();
            ApiToken = organization.ApiToken.Require().Reveal();
            RoomWithNoMissing = await env.CreateRoomAsync(
                platformRoomId: "C000000123",
                managedConversationsEnabled: true);
            await env.Settings.SetLastVerifiedMessageIdAsync(
                RoomWithNoMissing,
                messageId: "1642913722.918019",
                env.TestData.Abbot.User);
            var convo1 = await env.CreateConversationAsync(
                RoomWithNoMissing,
                firstMessageId: "1643015291.611399");
            var convo2 = await env.CreateConversationAsync(
                RoomWithNoMissing,
                firstMessageId: "1642913739.993191");
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                ApiToken,
                channel: RoomWithNoMissing.PlatformRoomId,
                oldest: "1642913722.918019",
                messages: GetMessages(env, convo1, convo2).ToList());
        }

        public IEnumerable<SlackMessage> GetMessages(TestEnvironmentWithData env, Conversation convo1, Conversation convo2)
        {
            // The API returns messages ordered from most recent to oldest, so we need to grab the first message.
            var foreignOrg = env.TestData.ForeignOrganization;
            yield return new()
            {
                Timestamp = "1643015291.611399",
                ThreadTimestamp = convo1.FirstMessageId,
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata(foreignOrg.PlatformId, null, null, null, null, false, false)
            };
            yield return new()
            {
                // Ignored because author is in same org.
                Timestamp = "1642913741.428980",
                ThreadTimestamp = "1642913741.428980",
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata("T008675309", null, null, null, null, false, false)
            };
            yield return new()
            {
                Timestamp = "1642913739.993191",
                ThreadTimestamp = convo2.FirstMessageId,
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata(foreignOrg.PlatformId, null, null, null, null, false, false)
            };
            yield return new()
            {
                // Ignored because ts != thread_ts (aka it's a reply).
                Timestamp = "1642913722.918020",
                ThreadTimestamp = "1642913719.111209",
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata(foreignOrg.PlatformId, null, null, null, null, false, false)
            };
        }
    }

    public class TestDataWithMissingConversations : TestDataNoMissing
    {
        public Room RoomWithMultipleMissingConversations { get; private set; } = null!;
        public Room RoomWithOneMissingConversation { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);

            RoomWithMultipleMissingConversations = await env.CreateRoomAsync(
                platformRoomId: "C0000008",
                managedConversationsEnabled: true);
            await env.Settings.SetLastVerifiedMessageIdAsync(
                RoomWithMultipleMissingConversations,
                messageId: "1642913741.428980",
                env.TestData.Abbot.User);
            RoomWithOneMissingConversation = await env.CreateRoomAsync(
                platformRoomId: "C0000010",
                managedConversationsEnabled: true);
            await env.Settings.SetLastVerifiedMessageIdAsync(
                RoomWithOneMissingConversation,
                messageId: "1642913776.493065",
                env.TestData.Abbot.User);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                ApiToken,
                channel: RoomWithMultipleMissingConversations.PlatformRoomId,
                oldest: "1642913741.428980",
                messages: GetRoomWithMultipleMissingConversationsMessages(env).ToList());
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                ApiToken,
                channel: RoomWithOneMissingConversation.PlatformRoomId,
                oldest: "1642913776.493065",
                messages: GetRoomWitOneMissingConversationsMessages(env).ToList());
        }

        static IEnumerable<SlackMessage> GetRoomWithMultipleMissingConversationsMessages(TestEnvironmentWithData env)
        {
            var organization = env.TestData.Organization;
            var foreignOrg = env.TestData.ForeignOrganization;
            var homeUser = env.TestData.User;
            var foreignUser = env.TestData.ForeignUser;
            yield return new()
            {
                // Foreign message
                Text = "Hello, I need help",
                User = foreignUser.PlatformUserId,
                Timestamp = "1642913779.429981",
                ThreadTimestamp = "1642913779.429981",
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata(foreignOrg.PlatformId, null, null, null, null, false, false)
            };

            yield return new()
            {
                // Completely unknown user message
                Text = "I have an identity crisis",
                User = "U00000000001",
                Timestamp = "1642913776.372574",
                ThreadTimestamp = "1642913776.372574",
                TeamId = "T008675309",
            };

            yield return new()
            {
                // Unknown Foreign message
                Text = "Please help me",
                User = foreignUser.PlatformUserId,
                Timestamp = "1642913774.150735",
                ThreadTimestamp = "1642913774.150735",
                TeamId = "T008675309",
            };

            yield return new()
            {
                // Ignored because author is in same org and not a guest.
                Text = "I work here",
                User = homeUser.PlatformUserId,
                Timestamp = "1642913771.594740",
                ThreadTimestamp = "1642913771.594740",
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata("T008675309", null, null, null, null, false, false)
            };

            yield return new()
            {
                // We know the user is a guest without looking it up in our db
                Text = "Hello, I need help",
                User = env.TestData.GuestUser.PlatformUserId,
                Timestamp = "1642913769.708755",
                ThreadTimestamp = null, // It's possible we'll get a null value here for a top-level message with no replies.
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata("T008675309", null, null, null, null, true, false)
            };

            yield return new()
            {
                // Ignored because ts != thread_ts (aka it's a reply).
                Text = "I'm replying to your message",
                User = env.TestData.ForeignUser.PlatformUserId,
                Timestamp = "1642913764.727179",
                ThreadTimestamp = "1642913765.287688",
                TeamId = organization.PlatformId,
                UserProfile = new UserProfileMetadata("T008675309", null, null, null, null, false, false)
            };

            yield return new()
            {
                // Unknown Guest.
                Text = "I'm not anyone you know",
                User = env.TestData.GuestUser.PlatformUserId,
                Timestamp = "1642913759.423639",
                ThreadTimestamp = "1642913759.423639",
                TeamId = "T008675309",
            };
        }

        static IEnumerable<SlackMessage> GetRoomWitOneMissingConversationsMessages(TestEnvironmentWithData env)
        {
            yield return new()
            {
                // Foreign message
                Text = "Hello, I need help",
                User = env.TestData.ForeignUser.PlatformUserId,
                Timestamp = "1642913778.112116",
                ThreadTimestamp = "1642913778.112116",
                TeamId = "T008675309",
                UserProfile = new UserProfileMetadata(env.TestData.ForeignOrganization.PlatformId, null, null, null, null, false, false)
            };
        }
    }

    public class TestDataWithMissingConversationsWithException : TestDataWithMissingConversations
    {
        public Room RoomThrowsNonRetryApiException { get; private set; } = null!;
        public Room RoomThrowsException { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);

            RoomThrowsNonRetryApiException = await env.CreateRoomAsync(
                platformRoomId: "C000000042",
                managedConversationsEnabled: true);
            RoomThrowsException = await env.CreateRoomAsync(
                platformRoomId: "C000000099",
                managedConversationsEnabled: true);

            await env.Settings.SetLastVerifiedMessageIdAsync(
                RoomThrowsNonRetryApiException,
                messageId: "1642913579.934211",
                env.TestData.Abbot.User);
            await env.Settings.SetLastVerifiedMessageIdAsync(
                RoomThrowsException,
                messageId: "1642913421.621029",
                env.TestData.Abbot.User);
            var apiException = RefitTestHelpers.CreateApiException(
                HttpStatusCode.InternalServerError,
                HttpMethod.Post,
                uri: "https://example.com",
                payload: new { error = "Houston, we have a problem" });
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                ApiToken,
                channel: "C000000042",
                oldest: "1642913579.934211",
                exception: apiException);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                ApiToken,
                channel: "C000000099",
                oldest: "1642913421.621029",
                exception: new InvalidOperationException("Something broked."));
        }
    }

    public class TestDataWithOperationCancelledException : TestDataWithMissingConversations
    {
        public Room RoomThrowsException { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);

            RoomThrowsException = await env.CreateRoomAsync(
                platformRoomId: "C000000123",
                managedConversationsEnabled: true);

            await env.Settings.SetLastVerifiedMessageIdAsync(
                RoomThrowsException,
                messageId: "1642913421.621029",
                env.TestData.Abbot.User);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                ApiToken,
                channel: "C000000123",
                oldest: "1642913421.621029",
                exception: new OperationCanceledException());
        }
    }
}

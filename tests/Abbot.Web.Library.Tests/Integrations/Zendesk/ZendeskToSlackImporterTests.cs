using System.Diagnostics;
using Abbot.Common.TestHelpers;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Models;
using Serious.Abbot.Signals;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.TestHelpers;

namespace Abbot.Web.Library.Tests.Integrations.Zendesk;

public class ZendeskToSlackImporterTests
{
    public class TheQueueZendeskCommentSyncMethod
    {
        [Fact]
        public void EnqueuesCommentSyncJob()
        {
            var env = TestEnvironment.Create();
            var syncer = env.Activate<ZendeskToSlackImporter>();
            syncer.QueueZendeskCommentImport(
                env.TestData.ForeignOrganization,
                new ZendeskTicketLink("foo", 42),
                "Open",
                1234567890);

            var job = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            Assert.Equal(typeof(ZendeskToSlackImporter), job.Job.Type);
            Assert.Equal(typeof(ZendeskToSlackImporter), job.Job.Method.DeclaringType);
            Assert.Equal(nameof(ZendeskToSlackImporter.ZendeskCommentSyncJob), job.Job.Method.Name);
            Assert.Equal(new[]
                {
                    env.TestData.ForeignOrganization.Id, "https://foo.zendesk.com/api/v2/tickets/42.json",
                    "Open",
                    1234567890L,
                    (object?)null, // Hangfire fills this in.
                },
                job.Job.Args);
        }

        [Fact]
        public async Task DoesNotEnqueueSyncJobIfOrganizationDisabled()
        {
            var env = TestEnvironment.Create();
            var syncer = env.Activate<ZendeskToSlackImporter>();
            env.TestData.ForeignOrganization.Enabled = false;
            await env.Db.SaveChangesAsync();

            syncer.QueueZendeskCommentImport(
                env.TestData.ForeignOrganization,
                new ZendeskTicketLink("foo", 42),
                "Open",
                1234567890);

            Assert.Empty(env.BackgroundJobClient.EnqueuedJobs);
        }
    }

    public class TheTheZendeskCommentSyncJobMethodMethod
    {
        [Theory]
        [InlineData("Solved")]
        [InlineData("Closed")]
        public async Task WithoutCommentClosesSolvedOrClosedCaseWithLinkedIdentityAndStoresTicketState(string ticketStatus)
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var organization = env.TestData.Organization;
            var theSolver = await env.CreateMemberInAgentRoleAsync();
            env.Clock.Freeze();
            var integration = await env.Integrations.EnableAsync(organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });
            await env.LinkedIdentities.LinkIdentityAsync(
                organization,
                theSolver,
                LinkedIdentityType.Zendesk,
                $"https://{ticketLink.Subdomain}.zendesk.com/api/v2/users/1234567890.json");
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());
            var syncer = env.Activate<ZendeskToSlackImporter>();

            await syncer.ZendeskCommentSyncJob(
                organization.Id,
                ticketLink.ApiUrl.ToString(),
                ticketStatus,
                zendeskUserId: 1234567890,
                CreateTestPerformContext(organization, ticketLink));

            // Verify messages posted to Slack
            await env.ReloadAsync(conversation);
            await env.Db.Entry(conversation).Collection(c => c.Events).LoadAsync();
            Assert.Equal(ConversationState.Closed, conversation.State);
            var lastEvent = conversation.Events.OfType<StateChangedEvent>().Last();
            Assert.Equal(theSolver.Id, lastEvent.MemberId);
            Assert.Equal(ConversationState.Closed, lastEvent.NewState);
            var storedState = await env.Settings.GetAsync(
                SettingsScope.Conversation(conversation),
                "ZendeskTicketStatus");
            Assert.Equal(ticketStatus, storedState?.Value);

            env.SignalHandler.AssertRaised(
                SystemSignal.TicketStateChangedSignal.Name,
                (ticketLink with { Status = ticketStatus }).ToJson(),
                room.PlatformRoomId,
                env.TestData.Member,
                MessageInfo.FromConversation(conversation));

            Assert.Empty(env.ConversationPublisher.PublishedMessages);
        }

        [Fact]
        public async Task WithoutCommentClosesSolvedCaseAndLinksIdentity()
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var organization = env.TestData.Organization;
            var theSolver = await env.CreateMemberInAgentRoleAsync(email: "sherlock@example.com");
            env.Clock.Freeze();
            var integration = await env.Integrations.EnableAsync(organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());

            var client = env.ZendeskClientFactory.ClientFor(ticketLink.Subdomain);
            client.AddUser(new ZendeskUser
            {
                Id = 1234567890,
                Email = "sherlock@example.com",
            });
            var syncer = env.Activate<ZendeskToSlackImporter>();

            await syncer.ZendeskCommentSyncJob(
                organization.Id,
                ticketLink.ApiUrl.ToString(),
                ticketStatus: "Solved",
                zendeskUserId: 1234567890,
                CreateTestPerformContext(organization, ticketLink));

            // Verify messages posted to Slack
            await env.ReloadAsync(conversation);
            await env.Db.Entry(conversation).Collection(c => c.Events).LoadAsync();
            Assert.Equal(ConversationState.Closed, conversation.State);
            var lastEvent = conversation.Events.OfType<StateChangedEvent>().Last();
            Assert.Equal(theSolver.Id, lastEvent.MemberId);
            Assert.Equal(ConversationState.Closed, lastEvent.NewState);

            Assert.Empty(env.ConversationPublisher.PublishedMessages);
        }

        [Fact]
        public async Task WithoutCommentClosesSolvedCaseWithAbbotWhenIdentityNotLinked()
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var organization = env.TestData.Organization;
            env.Clock.Freeze();
            var integration = await env.Integrations.EnableAsync(organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());
            env.ZendeskClientFactory.ClientFor(ticketLink.Subdomain).AddUser(new ZendeskUser
            {
                Id = 1234567890,
                Email = "some-rando@example.com"
            });
            var syncer = env.Activate<ZendeskToSlackImporter>();

            await syncer.ZendeskCommentSyncJob(
                organization.Id,
                ticketLink.ApiUrl.ToString(),
                ticketStatus: "Solved",
                zendeskUserId: 1234567890,
                CreateTestPerformContext(organization, ticketLink));

            // Verify status changed to Closed.
            await env.ReloadAsync(conversation);
            await env.Db.Entry(conversation).Collection(c => c.Events).LoadAsync();
            Assert.Equal(ConversationState.Closed, conversation.State);
            var lastEvent = conversation.Events.OfType<StateChangedEvent>().Last();
            Assert.True(lastEvent.Member.IsAbbot());
            Assert.Equal(ConversationState.Closed, lastEvent.NewState);

            Assert.Empty(env.ConversationPublisher.PublishedMessages);
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Open")]
        public async Task WithoutCommentWithNonSolvedStateDoesNotCloseConversation(string status)
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var organization = env.TestData.Organization;
            var theSolver = await env.CreateMemberInAgentRoleAsync();
            env.Clock.Freeze();
            var integration = await env.Integrations.EnableAsync(organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });
            await env.LinkedIdentities.LinkIdentityAsync(
                organization,
                theSolver,
                LinkedIdentityType.Zendesk,
                $"https://{ticketLink.Subdomain}.zendesk.com/api/v2/users/1234567890.json");
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());
            var syncer = env.Activate<ZendeskToSlackImporter>();

            await syncer.ZendeskCommentSyncJob(
                organization.Id,
                ticketLink.ApiUrl.ToString(),
                ticketStatus: status,
                zendeskUserId: 1234567890,
                CreateTestPerformContext(organization, ticketLink));

            await env.ReloadAsync(conversation);
            await env.Db.Entry(conversation).Collection(c => c.Events).LoadAsync();
            Assert.Equal(ConversationState.New, conversation.State);

            Assert.Empty(env.ConversationPublisher.PublishedMessages);
        }

        [Fact]
        public async Task SerializesConcurrentInvocations()
        {
            var env = TestEnvironment.Create();
            var syncer = env.Activate<ZendeskToSlackImporter>();
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var lockKey = $"{nameof(ZendeskToSlackImporter)}:{env.TestData.Organization.Id}:{ticketLink.ApiUrl}";
            var storageConnection = Substitute.For<IStorageConnection>();
            var backgroundJob = new BackgroundJob("42", null!, DateTime.Now);
            var performContext =
                new PerformContext(null, storageConnection, backgroundJob, new JobCancellationToken(false));

            var syncPointFirst = new SyncPoint();
            var syncPointSecond = new SyncPoint();
            var callCount = 0;
            storageConnection.AcquireDistributedLock(lockKey, TimeSpan.FromSeconds(10))
                .Returns(_ => {
                    callCount++;
                    if (callCount == 1)
                    {
                        // We gotta block. The Hangfire API doesn't return a Task :(
                        syncPointFirst.WaitToContinue().Wait(TimeSpan.FromSeconds(5));
                        return Disposable.Create(() => {
                            // Release the second invocation to run.
                            syncPointSecond.Continue();
                        });
                    }

                    // This is the second invocation. Wait until we get released.
                    syncPointSecond.WaitToContinue().Wait(TimeSpan.FromSeconds(5));
                    return null;
                });

            var firstJob = Task.Run(() =>
                syncer.ZendeskCommentSyncJob(env.TestData.Organization.Id,
                    ticketLink.ApiUrl.ToString(),
                    performContext));

            // We're blocked waiting on the lock. Wait until we reach the sync point.
            Assert.False(firstJob.IsCompletedSuccessfully);
            await syncPointFirst.WaitForSyncPoint().WaitAsync(TimeSpan.FromSeconds(5));

            // Now we know the job has acquired the lock. Try to start another invocation.
            // Then wait until _it's_ also blocked on the lock.
            var secondJob = Task.Run(() =>
                syncer.ZendeskCommentSyncJob(env.TestData.Organization.Id,
                    ticketLink.ApiUrl.ToString(),
                    performContext));

            Assert.False(secondJob.IsCompletedSuccessfully);
            await syncPointSecond.WaitForSyncPoint().WaitAsync(TimeSpan.FromSeconds(5));

            // Now both invocations are blocked on the lock.
            // Release the first one. We're just going to let
            // Since we haven't configured the integration, it will trigger an UnreachableException.
            // That's fine, since we're not trying to test the entire code path, just the locking.
            syncPointFirst.Continue();
            await Assert.ThrowsAsync<UnreachableException>(() => firstJob);

            // And the second job should finish after it.
            await Assert.ThrowsAsync<UnreachableException>(() => secondJob);
        }

        [Fact]
        public async Task NoOpsIfNoConversationForTicket()
        {
            var env = TestEnvironment.Create();
            var syncer = env.Activate<ZendeskToSlackImporter>();
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var performContext = CreateTestPerformContext(env.TestData.Organization, ticketLink);
            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "foo",
                    ApiToken = env.Secret("the-token"),
                });

            await syncer.ZendeskCommentSyncJob(env.TestData.Organization.Id,
                ticketLink.ApiUrl.ToString(),
                performContext);

            Assert.True(env.LoggerProvider.DidLog<ZendeskToSlackImporter>(
                nameof(ZendeskToSlackImporterLoggerExtensions.NoConversationForTicket),
                new Dictionary<string, object>()
                {
                    {"ZendeskTicketUrl", ticketLink.ApiUrl.ToString()},
                }));

            Assert.Empty(env.ConversationPublisher.PublishedMessages);
        }

        [Fact]
        public async Task SyncsEntireTicketIfNoCommentMarker()
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var commentsFixture = new TicketCommentsFixture();
            var env = TestEnvironmentBuilder.Create()
                .Configure(commentsFixture.Install(ticketLink.Subdomain, ticketLink.TicketId))
                .Build();
            env.Clock.Freeze();

            var firstUserLink = new ZendeskUserLink(ticketLink.Subdomain, 1);
            var secondUserLink = new ZendeskUserLink(ticketLink.Subdomain, 2);
            commentsFixture.AddUser(firstUserLink, new("First User", "https://example.com/first", null));
            commentsFixture.AddUser(secondUserLink, new("Second User", null, env.TestData.Member));
            commentsFixture.AddComment(1, "The first comment, from no client", null, true);
            commentsFixture.AddComment(2, "The second comment, from Abbot",
                $"{ZendeskClientFactory.UserAgentProductName}/42.42.42", true);
            commentsFixture.AddComment(1, "The third comment, internal", null, false);
            commentsFixture.AddComment(1, "The fourth comment, from Safari", "Safari/42.42.42", true, new List<Attachment>
            {
                new() { ContentType = "image/png", ContentUrl = "https://example.com/image.png", FileName = "image.png" },
                new() { ContentType = "image/jpeg", ContentUrl = "https://example.com/image.jpg", FileName = "image.jpg" },
                new() { ContentType = "image/gif", ContentUrl = "https://example.com/image.gif", FileName = "image.gif" },
                new() { ContentType = "image/svg", ContentUrl = "https://example.com/image.svg", FileName = "image.svg" },
                new() { ContentType = "text/csv", ContentUrl = "https://example.com/text.csv", FileName = "text.csv"},
            });
            commentsFixture.AddComment(2, "The fifth comment, from no client", null, true);

            var syncer = env.Activate<ZendeskToSlackImporter>();
            var performContext = CreateTestPerformContext(env.TestData.Organization, ticketLink);
            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());

            await syncer.ZendeskCommentSyncJob(env.TestData.Organization.Id,
                ticketLink.ApiUrl.ToString(),
                performContext);

            // Verify messages posted to Slack
            Assert.Collection(env.SlackApi.PostedMessages,
                SlackMessageAsserter(ticketLink,
                    "The first comment, from no client",
                    "First User (from Zendesk)",
                    "https://example.com/first"),
                // The second comment is from Abbot, so we don't post it.
                SlackMessageWithAttachmentsAsserter(ticketLink,
                    "The fourth comment, from Safari",
                    "First User (from Zendesk)",
                    "https://example.com/first",
                    new List<Attachment>
                    {
                        new() { ContentType = "image/svg", ContentUrl = "https://example.com/image.svg", FileName = "image.svg" },
                        new() { ContentType = "text/csv", ContentUrl = "https://example.com/text.csv", FileName = "text.csv"},
                    },
                    new List<Attachment>
                    {
                        new() { ContentType = "image/png", ContentUrl = "https://example.com/image.png", FileName = "image.png" },
                        new() { ContentType = "image/jpeg", ContentUrl = "https://example.com/image.jpg", FileName = "image.jpg" },
                        new() { ContentType = "image/gif", ContentUrl = "https://example.com/image.gif", FileName = "image.gif" },
                    }),
                SlackMessageAsserter(ticketLink,
                    "The fifth comment, from no client",
                    "Second User",
                    null));

            // Did we save timeline events?
            Assert.Collection((await env.Conversations.GetTimelineAsync(convo)).OfType<MessagePostedEvent>(),
                MessagePostedEventAsserter(env.Clock.UtcNow,
                    1,
                    ticketLink,
                    firstUserLink,
                    "First User",
                    env.TestData.Abbot),
                MessagePostedEventAsserter(env.Clock.UtcNow,
                    2,
                    ticketLink,
                    firstUserLink,
                    "First User",
                    env.TestData.Abbot),
                MessagePostedEventAsserter(env.Clock.UtcNow,
                    3,
                    ticketLink,
                    secondUserLink,
                    "Second User",
                    env.TestData.Member));

            // Did we save the comment marker?
            var commentMarker = await env.Settings.GetAsync(SettingsScope.Conversation(convo),
                ZendeskToSlackImporter.CommentMarkerSettingName);
            Assert.Equal("4", commentMarker?.Value);

            // Someday: test better; maybe Verify?
            Assert.Equal(3, env.ConversationPublisher.PublishedMessages.Count);
            Assert.All(
                env.ConversationPublisher.PublishedMessages,
                published => Assert.Equal(typeof(NewMessageInConversation), published.MessageType));
        }

        [Fact]
        public async Task UsesCommentMarkerIfOneHasBeenSaved()
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var commentsFixture = new TicketCommentsFixture();
            var env = TestEnvironmentBuilder.Create()
                .Configure(commentsFixture.Install(ticketLink.Subdomain, ticketLink.TicketId))
                .Build();
            env.Clock.Freeze();

            var firstUserLink = new ZendeskUserLink(ticketLink.Subdomain, 1);
            var secondUserLink = new ZendeskUserLink(ticketLink.Subdomain, 2);
            commentsFixture.AddUser(firstUserLink, new("First User", "https://example.com/first", null));
            commentsFixture.AddUser(secondUserLink, new("Second User", null, env.TestData.Member));
            commentsFixture.AddComment(1, "The first comment, from no client", null, true);
            commentsFixture.AddComment(2, "The second comment, from Abbot",
                $"{ZendeskClientFactory.UserAgentProductName}/42.42.42", true);
            commentsFixture.AddComment(1, "The third comment, from Safari", "Safari/42.42.42", true);
            commentsFixture.AddComment(2, "The fourth comment, from no client", null, true);

            var syncer = env.Activate<ZendeskToSlackImporter>();
            var performContext = CreateTestPerformContext(env.TestData.Organization, ticketLink);
            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());

            await env.Settings.SetAsync(SettingsScope.Conversation(convo),
                ZendeskToSlackImporter.CommentMarkerSettingName,
                "1", // This is "The second comment, from Abbot"
                env.TestData.Abbot.User);

            await syncer.ZendeskCommentSyncJob(env.TestData.Organization.Id,
                ticketLink.ApiUrl.ToString(),
                performContext);

            // Verify messages posted to Slack
            Assert.Collection(env.SlackApi.PostedMessages,
                SlackMessageAsserter(ticketLink,
                    "The third comment, from Safari",
                    "First User (from Zendesk)",
                    "https://example.com/first"),
                SlackMessageAsserter(ticketLink,
                    "The fourth comment, from no client",
                    "Second User",
                    null));

            // Did we save timeline events?
            Assert.Collection((await env.Conversations.GetTimelineAsync(convo)).OfType<MessagePostedEvent>(),
                MessagePostedEventAsserter(env.Clock.UtcNow,
                    1,
                    ticketLink,
                    firstUserLink,
                    "First User",
                    env.TestData.Abbot),
                MessagePostedEventAsserter(env.Clock.UtcNow,
                    2,
                    ticketLink,
                    secondUserLink,
                    "Second User",
                    env.TestData.Member));

            // Did we save the comment marker?
            var commentMarker = await env.Settings.GetAsync(SettingsScope.Conversation(convo),
                ZendeskToSlackImporter.CommentMarkerSettingName);
            Assert.Equal("3", commentMarker?.Value);

            // Someday: test better; maybe Verify?
            Assert.Equal(2, env.ConversationPublisher.PublishedMessages.Count);
            Assert.All(
                env.ConversationPublisher.PublishedMessages,
                published => Assert.Equal(typeof(NewMessageInConversation), published.MessageType));
        }

        [Theory]
        [InlineData("No Matching Slack User", ConversationState.NeedsResponse)]
        [InlineData("Home Org User", ConversationState.Waiting)]
        [InlineData("Home Org Guest", ConversationState.NeedsResponse)]
        [InlineData("Foreign Org User", ConversationState.NeedsResponse)]
        public async Task UpdatesConversationStateCorrectly(string authorDisplayName, ConversationState expectedState)
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var commentsFixture = new TicketCommentsFixture();
            var env = TestEnvironmentBuilder.Create()
                .Configure(commentsFixture.Install(ticketLink.Subdomain, ticketLink.TicketId))
                .Build();
            env.Clock.Freeze();

            var authorTypes = new List<SlackMessageAuthor>
            {
                new("No Matching Slack User", null, null),
                new("Home Org User", null, env.TestData.Member),
                new("Home Org Guest", null, env.TestData.Guest),
                new("Foreign Org User", null, env.TestData.ForeignMember),
            };

            var selectedAuthor = authorTypes.Single(a => a.DisplayName == authorDisplayName);

            commentsFixture.AddUser(new ZendeskUserLink(ticketLink.Subdomain, 1), selectedAuthor);
            commentsFixture.AddComment(1, "The comment", null, true);

            var syncer = env.Activate<ZendeskToSlackImporter>();
            var performContext = CreateTestPerformContext(env.TestData.Organization, ticketLink);
            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);

            // Put these in the "Waiting" state to begin with
            await env.Conversations.UpdateForNewMessageAsync(convo,
                new MessagePostedEvent { MessageId = "42" },
                env.CreateConversationMessage(convo, messageId: "42"),
                false);
            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Waiting, convo.State);

            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());

            await syncer.ZendeskCommentSyncJob(
                env.TestData.Organization.Id,
                ticketLink.ApiUrl.ToString(),
                performContext);

            await env.ReloadAsync(convo);
            Assert.Equal(expectedState, convo.State);
        }

        [Fact]
        public async Task ClosesConversationWhenStatusChangeToSolved()
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var commentsFixture = new TicketCommentsFixture();
            var env = TestEnvironmentBuilder.Create()
                .Configure(commentsFixture.Install(ticketLink.Subdomain, ticketLink.TicketId))
                .Build();
            env.Clock.Freeze();

            var selectedAuthor = new SlackMessageAuthor("Home Org User", null, env.TestData.Member);

            commentsFixture.AddUser(new ZendeskUserLink(ticketLink.Subdomain, 1), selectedAuthor);
            commentsFixture.AddComment(1, "The comment", null, true);

            var syncer = env.Activate<ZendeskToSlackImporter>();
            var performContext = CreateTestPerformContext(env.TestData.Organization, ticketLink);
            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);

            // Put these in the "Waiting" state to begin with
            await env.Conversations.UpdateForNewMessageAsync(convo,
                new MessagePostedEvent { MessageId = "42" },
                env.CreateConversationMessage(convo, messageId: "42"),
                false);
            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Waiting, convo.State);

            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());

            await syncer.ZendeskCommentSyncJob(
                env.TestData.Organization.Id,
                ticketLink.ApiUrl.ToString(),
                "Solved",
                1,
                performContext);

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Closed, convo.State);
        }

        [Fact]
        public async Task DoesNotCloseConversationWhenStatusAlreadySolved()
        {
            var ticketLink = new ZendeskTicketLink("foo", 42);
            var commentsFixture = new TicketCommentsFixture();
            var env = TestEnvironmentBuilder.Create()
                .Configure(commentsFixture.Install(ticketLink.Subdomain, ticketLink.TicketId))
                .Build();
            env.Clock.Freeze();

            var selectedAuthor = new SlackMessageAuthor("Home Org User", null, env.TestData.Member);

            commentsFixture.AddUser(new ZendeskUserLink(ticketLink.Subdomain, 1), selectedAuthor);
            commentsFixture.AddComment(1, "The comment", null, true);

            var syncer = env.Activate<ZendeskToSlackImporter>();
            var performContext = CreateTestPerformContext(env.TestData.Organization, ticketLink);
            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = ticketLink.Subdomain,
                    ApiToken = env.Secret("the-token"),
                });

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var scope = SettingsScope.Conversation(convo);
            await env.Settings.SetAsync(scope, "ZendeskTicketStatus", "Solved", env.TestData.Abbot.User);

            // Put these in the "Waiting" state to begin with
            await env.Conversations.UpdateForNewMessageAsync(convo,
                new MessagePostedEvent { MessageId = "42" },
                env.CreateConversationMessage(convo, messageId: "42"),
                false);
            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Waiting, convo.State);

            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                ticketLink.ApiUrl.ToString());

            await syncer.ZendeskCommentSyncJob(
                env.TestData.Organization.Id,
                ticketLink.ApiUrl.ToString(),
                "Solved",
                1,
                performContext);

            await env.ReloadAsync(convo);
            Assert.Equal(ConversationState.Waiting, convo.State);
        }

        static Action<MessagePostedEvent> MessagePostedEventAsserter(DateTime timestamp, int messageSequenceNumber, ZendeskTicketLink ticketLink, ZendeskUserLink authorLink, string externalAuthor, Member actor)
        {
            return evt => {
                var ts = new SlackTimestamp(timestamp, $"{messageSequenceNumber}:D6");
                Assert.Equal(ts.ToString(), evt.MessageId);
                Assert.NotNull(evt.MessageUrl);
                Assert.Equal("Zendesk", evt.ExternalSource);
                Assert.Equal(ticketLink.ApiUrl.ToString(), evt.ExternalMessageId);
                Assert.Equal(authorLink.ApiUrl.ToString(), evt.ExternalAuthorId);
                Assert.Equal(externalAuthor, evt.ExternalAuthor);
                Assert.Same(actor, evt.Member);
            };
        }

        static Action<MessageRequest> SlackMessageAsserter(ZendeskTicketLink ticket, string text, string userName,
            string? iconUrl)
        {
            return request => {
                Assert.Equal(text, request.Text);
                Assert.Equal(userName, request.UserName);
                Assert.Equal(iconUrl, request.IconUrl?.ToString());
                Assert.NotNull(request.Blocks);
                Assert.Collection(request.Blocks,
                    b => {
                        var section = Assert.IsType<Section>(b);
                        var mrkdwn = Assert.IsType<MrkdwnText>(section.Text);
                        Assert.Equal(text, mrkdwn.Text);
                    },
                    b => {
                        var context = Assert.IsType<Context>(b);
                        var element = Assert.Single(context.Elements);
                        var mrkdwn = Assert.IsType<MrkdwnText>(element);
                        Assert.Equal($"This comment was posted on the <{ticket.WebUrl}|linked Zendesk ticket>.",
                            mrkdwn.Text);
                    });
            };
        }

        static Action<MessageRequest> SlackMessageWithAttachmentsAsserter(
            ZendeskTicketLink ticket,
            string text,
            string userName,
            string? iconUrl,
            IReadOnlyList<Attachment> fileAttachments,
            IReadOnlyList<Attachment> imageAttachments)
        {
            return request => {
                Assert.Equal(text, request.Text);
                Assert.Equal(userName, request.UserName);
                Assert.Equal(iconUrl, request.IconUrl?.ToString());
                Assert.NotNull(request.Blocks);
                // 3 because we have 3 blocks: 1 for the text, 1 for the file attachments, and 1 for the Zendesk link.
                Assert.Equal(3 + imageAttachments.Count, request.Blocks.Count);
                var firstBlock = request.Blocks[0];
                var section = Assert.IsType<Section>(firstBlock);
                var mrkdwn = Assert.IsType<MrkdwnText>(section.Text);
                Assert.Equal(text, mrkdwn.Text);

                var imageIndex = 0;
                foreach (var imageAttachment in imageAttachments)
                {
                    var block = request.Blocks[imageIndex + 1];
                    var imageBlock = Assert.IsType<Image>(block);
                    Assert.NotNull(imageBlock.Title);
                    Assert.Equal(imageAttachment.FileName, imageBlock.Title.Text);
                    Assert.Equal(imageAttachment.ContentUrl, imageBlock.ImageUrl.ToString());
                    Assert.Equal(imageAttachment.FileName, imageBlock.AltText);
                    imageIndex++;
                }

                if (fileAttachments.Any())
                {
                    var fileAttachmentsSection = Assert.IsType<Section>(request.Blocks[^2]);
                    Assert.NotNull(fileAttachmentsSection.Text);
                    Assert.Equal("*File Attachments*", fileAttachmentsSection.Text.Text);
                    var fileFields = Assert.IsType<Section>(request.Blocks[^2]).Fields;
                    Assert.NotNull(fileFields);
                    Assert.Equal(fileAttachments.Count, fileFields.Count);
                    for (var i = 0; i < fileAttachments.Count; i++)
                    {
                        var field = fileFields[i];
                        var fileAttachment = fileAttachments[i];
                        Assert.Equal($"<{fileAttachment.ContentUrl}|{fileAttachment.FileName}>", field.Text);
                    }
                }

                var lastBlock = request.Blocks.Last();
                var context = Assert.IsType<Context>(lastBlock);
                var element = Assert.Single(context.Elements);
                var lastBlockText = Assert.IsType<MrkdwnText>(element);
                Assert.Equal($"This comment was posted on the <{ticket.WebUrl}|linked Zendesk ticket>.",
                    lastBlockText.Text);
            };
        }

        static PerformContext CreateTestPerformContext(Organization organization, ZendeskTicketLink ticketLink)
        {
            var lockKey = $"{nameof(ZendeskToSlackImporter)}:{organization.Id}:{ticketLink.ApiUrl.ToString()}";
            var storageConnection = Substitute.For<IStorageConnection>();
            var backgroundJob = new BackgroundJob("42", null!, DateTime.Now);
            var performContext =
                new PerformContext(null, storageConnection, backgroundJob, new JobCancellationToken(false));

            var locked = false;
            storageConnection.AcquireDistributedLock(lockKey, TimeSpan.FromSeconds(10))
                .Returns(_ => {
                    Assert.False(locked);
                    locked = true;
                    return Disposable.Create(() => { locked = false; });
                });

            return performContext;
        }

        class TicketCommentsFixture
        {
            readonly IList<Comment> _comments = new List<Comment>();

            readonly IDictionary<ZendeskUserLink, SlackMessageAuthor> _users =
                new Dictionary<ZendeskUserLink, SlackMessageAuthor>();

            public void AddUser(ZendeskUserLink userLink, SlackMessageAuthor author)
            {
                _users.Add(userLink, author);
            }

            public void AddComment(long authorId, string body, string? userAgent, bool publicReply)
                => AddComment(authorId, body, userAgent, publicReply, new List<Attachment>());

            public void AddComment(long authorId, string body, string? userAgent, bool publicReply, IReadOnlyList<Attachment> attachments)
            {
                var comment = new Comment
                {
                    AuthorId = authorId,
                    AuditId = 1000 + _comments.Count,
                    Id = _comments.Count,
                    Body = body,
                    Public = publicReply,
                    Attachments = attachments
                };

                if (userAgent is not null)
                {
                    comment.AuditMetadata = new AuditMetadata
                    {
                        System = new SystemMetadata
                        {
                            Client = userAgent
                        }
                    };
                }

                _comments.Add(comment);
            }

            public Action<TestEnvironmentBuilder<TestEnvironmentWithData>> Install(
                string subdomain,
                long ticketId
            ) => builder => {
                var client = Substitute.For<IZendeskClient>();
                client.ListTicketCommentsAsync(ticketId, Arg.Any<int>(), Arg.Any<string?>())
                    .Returns(call => {
                        var after = call.Arg<string?>();
                        var afterIndex = -1;
                        if (after is not null)
                        {
                            afterIndex = int.Parse(after);
                        }

                        var comments = new List<Comment>();
                        var meta = new PaginationMetadata();

                        // Ignore the page size, just return groups of 2 to ensure we're testing pagination.
                        var end = Math.Min(afterIndex + 2, _comments.Count - 1);
                        for (int i = afterIndex + 1; i <= end; i++)
                        {
                            comments.Add(_comments[i]);
                        }

                        meta.AfterCursor = end.ToString();
                        if (end < _comments.Count - 1)
                        {
                            meta.HasMore = true;
                        }

                        return new CommentListMessage
                        {
                            Body = comments,
                            Meta = meta,
                        };
                    });

                builder.Substitute<IZendeskClientFactory>(out var clientFactory);

                builder.Substitute<IZendeskResolver>(out var zendeskResolver);

                zendeskResolver.ResolveSlackMessageAuthorAsync(Arg.Any<IZendeskClient>(),
                    Arg.Any<Organization>(),
                    Arg.Any<ZendeskUserLink>())
                    .Returns(call => {
                        var userLink = call.Arg<ZendeskUserLink>();
                        return _users[userLink];
                    });

                clientFactory.CreateClient(Arg.Is<ZendeskSettings>(settings => settings.Subdomain == subdomain))
                    .Returns(client);
            };
        }
    }

    public class TheIsSlackSupportedImageMethod
    {
        [Theory]
        [InlineData("image/png", true)]
        [InlineData("image/jpeg", true)]
        [InlineData("image/gif", true)]
        [InlineData("image/bmp", false)]
        [InlineData("image/svg+xml", false)]
        [InlineData("", false)]
        public void SupportsSomeImageTypes(string contentType, bool expected)
        {
            var attachment = new Attachment
            {
                ContentType = contentType,
                ContentUrl = "https://example.com/image",
                FileName = "image",
                Size = Image.MaxUploadSize
            };

            var result = ZendeskToSlackImporter.IsSlackSupportedImage(attachment);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReturnsFalseForContentUrlTooLarge()
        {
            var contentUrlTemplate = "https://|.example.com/image.png";
            var filler = new string('a', 3000 - contentUrlTemplate.Length + 2);
            var contentUrl = contentUrlTemplate.Replace("|", filler);
            Assert.Equal(3001, contentUrl.Length);
            var attachment = new Attachment
            {
                ContentType = "image/png",
                ContentUrl = contentUrl,
                FileName = "image"
            };

            var result = ZendeskToSlackImporter.IsSlackSupportedImage(attachment);

            Assert.False(result);
        }

        [Fact]
        public void ReturnsFalseForContentSizeTooLarge()
        {
            var attachment = new Attachment
            {
                ContentType = "image/png",
                ContentUrl = "https://example.com/image.png",
                FileName = "image",
                Size = Image.MaxUploadSize + 1
            };

            var result = ZendeskToSlackImporter.IsSlackSupportedImage(attachment);

            Assert.False(result);
        }
    }

    public class TheAttachmentToImageMethod
    {
        [Fact]
        public void TruncatesTitle()
        {
            var attachment = new Attachment
            {
                ContentType = "image/png",
                ContentUrl = "https://example.com/image",
                FileName = new string('a', 2001)
            };

            var result = ZendeskToSlackImporter.AttachmentToImage(attachment);

            Assert.NotNull(result?.Title);
            Assert.Equal(2000, result.Title.Text.Length);
        }

        [Fact]
        public void TruncatesAltText()
        {
            var attachment = new Attachment
            {
                ContentType = "image/png",
                ContentUrl = "https://example.com/image",
                FileName = new string('a', 2001)
            };

            var result = ZendeskToSlackImporter.AttachmentToImage(attachment);

            Assert.NotNull(result?.AltText);
            Assert.Equal(2000, result.AltText.Length);
        }

        [Fact]
        public void ReturnsImageWithUnknownFileName()
        {
            var attachment = new Attachment
            {
                ContentType = "image/png",
                ContentUrl = "https://example.com/image",
                FileName = null // As far as we know, this never happens.
            };

            var result = ZendeskToSlackImporter.AttachmentToImage(attachment);

            Assert.NotNull(result);
            Assert.Equal("?", result.Title);
            Assert.Equal("?", result.AltText);
        }

        [Fact]
        public void ReturnsNullForNullContentUrl()
        {
            var attachment = new Attachment
            {
                ContentType = "image/png",
                ContentUrl = null,
                FileName = new string('a', 2001)
            };

            var result = ZendeskToSlackImporter.AttachmentToImage(attachment);

            Assert.Null(result);
        }
    }

    public class TheAttachmentToMrkdwnMethod
    {
        [Fact]
        public void ReturnsMrkdwnLinkTruncatedTo2000Characters()
        {
            var attachment = new Attachment
            {
                ContentUrl = "https://example.com/file",
                FileName = new string('a', 2001)
            };

            var result = ZendeskToSlackImporter.AttachmentToMrkdwnText(attachment);

            var expectedFileName = attachment.FileName.TruncateToLength(2000 - 3 - attachment.ContentUrl.Length);
            Assert.NotNull(result?.Text);
            Assert.Equal($"<https://example.com/file|{expectedFileName}>", result.Text);
            Assert.Equal(2000, result.Text.Length);
        }

        [Fact]
        public void ReturnsMrkdwnLinkWithUnknownFileName()
        {
            var attachment = new Attachment
            {
                ContentUrl = "https://example.com/file",
                FileName = null // As far as we know, this never happens.
            };

            var result = ZendeskToSlackImporter.AttachmentToMrkdwnText(attachment);

            Assert.Equal($"<https://example.com/file|?>", result?.Text);
        }

        [Fact]
        public void ReturnsNullIfContentUrlLargerThan2096Characters()
        {
            // There's just nothing we can do about this.
            var contentUrlTemplate = "https://|.example.com/image.png";
            var filler = new string('a', 2096 - contentUrlTemplate.Length + 2);
            var contentUrl = contentUrlTemplate.Replace("|", filler);
            Assert.Equal(2097, contentUrl.Length);
            var attachment = new Attachment
            {
                ContentUrl = contentUrl,
                FileName = "a"
            };

            var result = ZendeskToSlackImporter.AttachmentToMrkdwnText(attachment);

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsNullIfContentUrlNull()
        {
            // There's just nothing we can do about this.
            var attachment = new Attachment
            {
                ContentUrl = null,
                FileName = "a"
            };

            var result = ZendeskToSlackImporter.AttachmentToMrkdwnText(attachment);

            Assert.Null(result);
        }
    }
}

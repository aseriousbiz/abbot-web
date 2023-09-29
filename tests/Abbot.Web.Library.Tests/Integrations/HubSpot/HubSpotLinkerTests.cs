using System.Net;
using Abbot.Common.TestHelpers;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Cryptography;
using Serious.TestHelpers;

// TicketLinkerJob does not resolve HubSpotLinker directly
using TicketLinker = Serious.Abbot.Integrations.ITicketLinker<
        Serious.Abbot.Integrations.HubSpot.HubSpotSettings>;

public class HubSpotLinkerTests
{
    static readonly HubSpotSettings TestSettings = new()
    {
        AccessToken = new SecretString("hubspot-access-token", new FakeDataProtectionProvider()),
        TicketPipelineId = "42",
        NewTicketPipelineStageId = "24",
    };

    static Dictionary<string, object?> TestProperties => new()
    {
        ["subject"] = "Subject",
        ["content"] = "Description",
    };

    public class TheCreateTicketLinkAsyncMethod
    {
        [Theory]
        [InlineData(null, null, null)]
        [InlineData(null, 42, null)]
        [InlineData("form-guid", null, null)]
        [InlineData("form-guid", 42, null)]
        public async Task ThrowsTicketConfigurationExceptionIfNoTicketPipelineOrFirstStageConfigured(
            string? formGuid,
            string? pipelineId,
            string? stageId)
        {
            var env = TestEnvironment.Create(snapshot: true);

            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            if (formGuid is not null)
            {
                await env.Settings.SetHubSpotFormSettingsAsync(
                    new FormSettings(formGuid, "TICKET.content"),
                    organization,
                    actor.User);
            }

            var settings = new HubSpotSettings()
            {
                AccessToken = TestSettings.AccessToken,
                TicketPipelineId = pipelineId,
                NewTicketPipelineStageId = stageId,
            };
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);

            var actual = await Assert.ThrowsAsync<TicketConfigurationException>(() => task);

            Assert.Empty(env.HubSpotClientFactory.Clients);
            Assert.Empty(env.HubSpotClientFactory.HubSpotFormsClients);
            Assert.Equal(
                "Your organization does not have a Ticket Pipeline and Pipeline Stage configured.",
                actual.Message);
            Assert.Equal(TicketErrorReason.Configuration, actual.Reason);
        }

        [Fact]
        public async Task ThrowsIfCredentialsInvalid()
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.HubSpotClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.Unauthorized,
                HttpMethod.Post,
                "",
                new {
                    status = "error",
                    message = "Authentication credentials not found.",
                    category = "INVALID_AUTHENTICATION"
                });
            client.CreateTicketAsync(Arg.Any<CreateOrUpdateTicket>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.Unauthorized, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Null(ticketError.ExtraInfo);
        }

        [Theory]
        // Strip prefix
        [InlineData(
            """
            Property values were not valid: [{"isValid":false,"message":"Property "hs_ticket_category2" does not exist","error":"PROPERTY_DOESNT_EXIST","name":"hs_ticket_category2"}]
            """,
            """
            [{"isValid":false,"message":"Property "hs_ticket_category2" does not exist","error":"PROPERTY_DOESNT_EXIST","name":"hs_ticket_category2"}]
            """
        )]
        [InlineData(
            "Unrecognized error",
            """
            {"status":"error","message":"Unrecognized error","category":"VALIDATION_ERROR"}
            """)]
        public async Task ThrowsIfApiFailsWithApiExceptionContainingValidationError(string errorMessage, string expectedUserErrorInfo)
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.HubSpotClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.BadRequest,
                HttpMethod.Post,
                "",
                new {
                    status = "error",
                    message = errorMessage,
                    category = "VALIDATION_ERROR"
                });
            client.CreateTicketAsync(Arg.Any<CreateOrUpdateTicket>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.ApiError, ticketError.Reason);
            Assert.Equal(expectedUserErrorInfo, ticketError.UserErrorInfo);
            Assert.Equal(actual.Content, ticketError.ExtraInfo);
        }

        [Fact]
        public async Task ThrowsIfApiFailsWithApiExceptionContainingUnexpectedError()
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.HubSpotClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.BadRequest,
                HttpMethod.Post,
                "",
                new[]
                {
                    new { message = "Error 1" },
                    new { message = "Error 2" },
                });
            client.CreateTicketAsync(Arg.Any<CreateOrUpdateTicket>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.ApiError, ticketError.Reason);
            Assert.Equal(
                """
                [{"message":"Error 1"},{"message":"Error 2"}]
                """,
                ticketError.UserErrorInfo);
            Assert.Equal(actual.Content, ticketError.ExtraInfo);
        }

        [Fact]
        public async Task ThrowsIfApiFailsForOtherReason()
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);
            var client = env.HubSpotClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = new Exception("Boom");
            client.CreateTicketAsync(Arg.Any<CreateOrUpdateTicket>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<Exception>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.Unknown, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Null(ticketError.ExtraInfo);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        [InlineData(TestMemberType.HomeGuest)]
        public async Task CreatesTicketLink(TestMemberType actorType)
        {
            var builder = TestEnvironmentBuilder.Create();
            builder.Services.Configure<HubSpotOptions>(options => {
                options.TimelineEvents[TimelineEvents.LinkedSlackConversation] = "templateId_123";
            });
            var env = builder.Build();

            var now = env.Clock.Freeze();

            var actor = env.TestData.GetMember(actorType);
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var client = env.HubSpotClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            HubSpotTicket? response = null;
            client.CreateTicketAsync(Arg.Any<CreateOrUpdateTicket>())
                .Returns(call => {
                    var request = call.Arg<CreateOrUpdateTicket>();
                    response = new HubSpotTicket()
                    {
                        Id = "42",
                        Properties = request.Properties,
                        CreatedAt = env.Clock.UtcNow,
                        UpdatedAt = env.Clock.UtcNow,
                        Archived = false
                    };

                    return response;
                });

            TimelineEvent? timelineEvent = null;
            client.CreateTimelineEventAsync(Arg.Any<TimelineEvent>())
                .Returns(call => {
                    timelineEvent = call.Arg<TimelineEvent>();
                    return timelineEvent;
                });

            var linker = env.Get<TicketLinker>();

            var fields = TestProperties
                .With("priority", (object?)"HIGH")
                .With("tags", (object?)new[] { "tag1", "tag2" });

            var link = await linker.CreateTicketLinkAsync(
                integration,
                settings,
                fields,
                convo,
                actor);

            Assert.NotNull(link);
            Assert.Same(convo, link.Conversation);
            Assert.Equal(ConversationLinkType.HubSpotTicket, link.LinkType);
            Assert.Same(actor, link.CreatedBy);
            Assert.Equal(now, link.Created);

            Assert.NotNull(response);
            Assert.Equal($"https://app.hubspot.com/contacts/4321/ticket/42", link.ExternalId);
            Assert.Equal("Subject", response.Properties["subject"]);
            Assert.Equal("Description", response.Properties["content"]);
            Assert.Equal("HIGH", response.Properties["priority"]);
            Assert.Equal("tag1;tag2", response.Properties["tags"]);

            Assert.NotNull(timelineEvent);
            Assert.Equal(response.Id, timelineEvent.ObjectId);
            Assert.Equal("templateId_123", timelineEvent.EventTemplateId);
            Assert.Equal(
                room.PlatformRoomId,
                Assert.Contains("slackChannelID", timelineEvent.Tokens));
            Assert.Equal(
                room.Name,
                Assert.Contains("slackChannelName", timelineEvent.Tokens));
            Assert.Equal(
                convo.GetFirstMessageUrl(),
                Assert.Contains("slackThreadUrl", timelineEvent.Tokens));
        }

        [Fact]
        public async Task WithFormSettingsThrowsIfCredentialsInvalid()
        {
            var env = TestEnvironment.Create();

            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.conent"),
                organization,
                actor.User);

            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.HubSpotClientFactory.HubSpotFormsClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.Unauthorized,
                HttpMethod.Post,
                "",
                new {
                    status = "error",
                    message = "Authentication credentials not found.",
                    category = "INVALID_AUTHENTICATION"
                });
            client.SubmitAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<HubSpotFormSubmissionRequest>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.Unauthorized, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Null(ticketError.ExtraInfo);
        }

        [Fact]
        public async Task WithFormSettingsThrowsIfApiFailsWithApiExceptionContainingUnexpectedError()
        {
            var env = TestEnvironment.Create();

            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.conent"),
                organization,
                actor.User);

            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.HubSpotClientFactory.HubSpotFormsClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.BadRequest,
                HttpMethod.Post,
                "",
                new[]
                {
                    new { message = "Error 1" },
                    new { message = "Error 2" },
                });
            client.SubmitAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<HubSpotFormSubmissionRequest>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.ApiError, ticketError.Reason);
            Assert.Equal(
                """
                [{"message":"Error 1"},{"message":"Error 2"}]
                """,
                ticketError.UserErrorInfo);
            Assert.Equal(actual.Content, ticketError.ExtraInfo);
        }

        [Fact]
        public async Task WithFormSettingsThrowsIfApiFailsForOtherReason()
        {
            var env = TestEnvironment.Create();

            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.conent"),
                organization,
                actor.User);

            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);
            var client = env.HubSpotClientFactory.HubSpotFormsClientFor(TestSettings.AccessToken?.Reveal());
            var ex = new Exception("Boom");
            client.SubmitAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<HubSpotFormSubmissionRequest>())
                .Throws(ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<Exception>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.Unknown, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Null(ticketError.ExtraInfo);
        }

        [Fact]
        public async Task WithFormSettingsCreatesTicketAndSchedulesLink()
        {
            var builder = TestEnvironmentBuilder.Create();
            builder.Services.Configure<HubSpotOptions>(options => {
                options.TimelineEvents[TimelineEvents.LinkedSlackConversation] = "templateId_123";
            });

            var env = builder.Build();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.conent"),
                organization,
                actor.User);

            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "4321", enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var client = env.HubSpotClientFactory.HubSpotFormsClientFor(settings.AccessToken?.Reveal());
            HubSpotFormSubmissionRequest? submissionRequest = null;
            client.SubmitAsync(
                    4321,
                    formGuid: "form-guid",
                    Arg.Do<HubSpotFormSubmissionRequest>(req => submissionRequest = req))
                .Returns(new SubmitResponse(null, null, Array.Empty<HubSpotError>()));

            var linkerJob = env.Get<TicketLinker>();

            await linkerJob.CreateTicketLinkAsync(
                integration,
                settings,
                new Dictionary<string, object?>
                {
                    ["TICKET.a_custom_field"] = "a_custom_value",
                    ["TICKET.content"] = "Description",
                    ["TICKET.hs_ticket_priority"] = "HIGH",
                    ["TICKET.subject"] = "Subject",
                },
                convo,
                actor);

            Assert.NotNull(submissionRequest);

            Assert.Equal(new HubSpotField[] {
                new("TICKET.a_custom_field", "a_custom_value"),
                new("TICKET.content", "Description"),
                new("TICKET.hs_ticket_priority", "HIGH"),
                new("TICKET.subject", "Subject"),
            }, submissionRequest.Fields.OrderBy(p => p.Name).ToArray());

            await env.ReloadAsync(convo);
            await env.Db.Entry(convo).Collection(c => c.Links).LoadAsync();
            Assert.Empty(convo.Links);

            var jobState = env.BackgroundJobClient.DidEnqueue<HubSpotLinker>(
                linker => linker.LinkPendingConversationTicketAsync(
                    convo,
                    actor,
                    actor.Organization,
                    0));
            // Hangfire hard-codes DateTime.UtcNow
            Assert.Equal(
                DateTime.UtcNow + TimeSpan.FromSeconds(2),
                Assert.IsType<ScheduledState>(jobState).EnqueueAt,
                TimeSpan.FromSeconds(1));
        }
    }

    public class TestData : CommonTestData
    {
        public Conversation Conversation { get; private set; } = null!;

        public Room Room { get; private set; } = null!;

        public Integration Integration { get; private set; } = null!;

        public IHubSpotClient HubSpotClient { get; private set; } = null!;

        public const long PortalId = 1234567890;

        public const string PlatformRoomId = "C0000011010";

        public const string RoomName = "the-room";

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            Room = await env.CreateRoomAsync(name: RoomName, platformRoomId: PlatformRoomId, managedConversationsEnabled: true);
            Conversation = await env.CreateConversationAsync(Room, firstMessageId: "M0000000001");
            Integration = await env.Integrations.EnsureIntegrationAsync(
                env.TestData.Organization,
                IntegrationType.HubSpot,
                enabled: true);

            Integration.ExternalId = $"{PortalId}";
            await env.Integrations.SaveSettingsAsync(Integration, new HubSpotSettings
            {
                AccessToken = env.Secret("SECRET-HUBSPOT-TOKEN"),
            });

            HubSpotClient = env.HubSpotClientFactory.ClientFor("SECRET-HUBSPOT-TOKEN");
            await base.SeedAsync(env);
        }
    }

    public class TheLinkPendingConversationTicketAsyncMethod
    {
        [Fact]
        public async Task ReturnsExistingConversationLinkUrl()
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var conversation = env.TestData.Conversation;
            var externalId = $"https://app.hubspot.com/contacts/{TestData.PortalId}/ticket/1128116740";
            await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.HubSpotTicket,
                externalId);
            var actor = env.TestData.Member;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.content"),
                organization,
                actor.User);
            var hubSpotLinker = env.Activate<HubSpotLinker>();

            var conversationLink = await hubSpotLinker.LinkPendingConversationTicketAsync(
                conversation,
                actor,
                actor.Organization,
                0);

            Assert.Equal(externalId, conversationLink?.ExternalId);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        public async Task LinksConversationToTicketUsingSearchToken(TestMemberType testMemberType)
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var conversation = env.TestData.Conversation;
            var actor = testMemberType is TestMemberType.HomeMember
                ? env.TestData.Member
                : env.TestData.ForeignMember;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.content"),
                organization,
                actor.User);
            var client = env.TestData.HubSpotClient;
            SearchRequest? searchRequest = null;
            client.SearchAsync("tickets", Arg.Do<SearchRequest>(req => searchRequest = req))
                .Returns(new SearchResults(1, new HubSpotSearchResult[]
                {
                    new(
                        Id: "1128116740",
                        Properties: new Dictionary<string, string?>
                        {
                            ["content"] = "Ok, I need some help and you’re just the person to help me. SEARCH_TOKEN"
                        },
                        CreatedAt: "2022-09-27T20:02:15.585Z",
                        UpdatedAt: "2022-09-27T20:02:17.664Z",
                        Archived: false)
                }));
            client.GetAssociationsAsync(HubSpotObjectType.Ticket, 1128116740, HubSpotObjectType.Conversation)
                .Returns(new HubSpotApiResults<HubSpotAssociation>(new HubSpotAssociation[]
                {
                    new(99999999, new HubSpotAssociationType[]
                    {
                        new("HUBSPOT_DEFINED", 32, null)
                    })
                }));
            var hubSpotLinker = env.Activate<HubSpotLinker>();

            var conversationLink = await hubSpotLinker.LinkPendingConversationTicketAsync(
                conversation,
                actor,
                actor.Organization,
                0);

            Assert.NotNull(searchRequest);
            var (propertyName, searchOperator, value) = searchRequest.FilterGroups.Single().Filters.Single();
            Assert.Equal("content", propertyName);
            Assert.Equal(SearchOperator.ContainsToken, searchOperator);
            Assert.Equal("cnv_1_01957ff826ee7aa712478628ccdee58104bbc86192704a67f91887d43e453bf9", value);
            Assert.Equal(
                $"https://app.hubspot.com/contacts/{TestData.PortalId}/ticket/1128116740",
                conversationLink?.ExternalId);
            await env.ReloadAsync(conversation);
            var link = conversation.GetHubSpotLink();
            Assert.NotNull(link);
            Assert.Equal(99999999, link.ThreadId.GetValueOrDefault());
        }

        [Fact]
        public async Task ReturnsNullAndSchedulesAnotherLinkAttemptIfNoResultsFound()
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var conversation = env.TestData.Conversation;
            var actor = env.TestData.Member;
            await env.Settings.SetHubSpotFormSettingsAsync(
                new FormSettings("form-guid", "TICKET.content"),
                organization,
                actor.User);
            var client = env.TestData.HubSpotClient;
            client.SearchAsync("tickets", Arg.Any<SearchRequest>())
                .Returns(new SearchResults(0, Array.Empty<HubSpotSearchResult>()));
            var hubSpotLinker = env.Activate<HubSpotLinker>();

            var ticketUrl = await hubSpotLinker.LinkPendingConversationTicketAsync(
                conversation,
                actor,
                actor.Organization,
                2);

            Assert.Null(ticketUrl);
            var enqueuedJob = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            Assert.Equal(
                nameof(HubSpotLinker.LinkPendingConversationTicketAsync),
                enqueuedJob.Job.Method.Name);
            Assert.Equal(3, enqueuedJob.Job.Args.Last());
        }
    }

    public class TheCreateConversationTicketLinkAsyncMethod
    {
        [Fact]
        public async Task LinksConversationWhenNoTimelineEvents()
        {
            var env = TestEnvironment.Create<TestData>();
            var actor = env.TestData.Member;
            var conversation = env.TestData.Conversation;
            var client = env.TestData.HubSpotClient;
            TimelineEvent? timelineEvent = null;
            client.CreateTimelineEventAsync(Arg.Any<TimelineEvent>())
                .Returns(call => {
                    timelineEvent = call.Arg<TimelineEvent>();
                    return timelineEvent;
                });
            var linker = env.Activate<HubSpotLinker>();

            var link = await linker.CreateConversationTicketLinkAsync(
                ticketId: 8675309,
                portalId: TestData.PortalId,
                conversation,
                client,
                actor);

            Assert.Equal($"https://app.hubspot.com/contacts/{TestData.PortalId}/ticket/8675309", link.ExternalId);
            Assert.Null(timelineEvent);
        }

        [Fact]
        public async Task CreatesTimelineEventAndLinksConversation()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .Configure<HubSpotOptions>(o =>
                    o.TimelineEvents = new Dictionary<string, string>
                    {
                        ["LinkedSlackConversation"] = "4815162342"
                    })
                .Build();
            var actor = env.TestData.Member;
            var conversation = env.TestData.Conversation;
            var client = env.TestData.HubSpotClient;
            TimelineEvent? timelineEvent = null;
            client.CreateTimelineEventAsync(Arg.Any<TimelineEvent>())
                .Returns(call => {
                    timelineEvent = call.Arg<TimelineEvent>();
                    return timelineEvent;
                });
            var linker = env.Activate<HubSpotLinker>();

            var link = await linker.CreateConversationTicketLinkAsync(
                ticketId: 8675309,
                portalId: TestData.PortalId,
                conversation,
                client,
                actor);

            Assert.Equal($"https://app.hubspot.com/contacts/{TestData.PortalId}/ticket/8675309", link.ExternalId);
            Assert.NotNull(timelineEvent);
            Assert.Equal("4815162342", timelineEvent.EventTemplateId);
            Assert.Equal("8675309", timelineEvent.ObjectId);
            Assert.Collection(timelineEvent.Tokens,
                kvp => Assert.Equal(("slackChannelID", TestData.PlatformRoomId), (kvp.Key, kvp.Value)),
                kvp => Assert.Equal(("slackChannelName", TestData.RoomName), (kvp.Key, kvp.Value)),
                kvp => Assert.Equal(("slackThreadUrl", "https://testorg.example.com/archives/C0000011010/pM0000000001"), (kvp.Key, kvp.Value)));
        }
    }
}

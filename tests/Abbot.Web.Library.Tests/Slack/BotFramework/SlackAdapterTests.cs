using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Configuration;
using Serious.Abbot.Exceptions;
using Serious.Cryptography;
using Serious.Slack;
using Serious.Slack.AspNetCore;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Events;
using Serious.Slack.Tests.Fakes;
using Serious.TestHelpers;
using IBot = Microsoft.Bot.Builder.IBot;

public class SlackAdapterTests
{
    public class TheProcessAsyncMethod
    {
        [Fact]
        public async Task ReturnsToUrlVerificationEventWithPlainTextContentResultWithVerificationChallenge()
        {
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var urlVerificationEvent = new {
                token = "REDACTED",
                type = "url_verification",
                challenge = "I challenge you to return this",
            };
            var requestBody = JsonConvert.SerializeObject(urlVerificationEvent);
            var bot = Substitute.For<IBot>();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                new FakeEventQueueClient(),
                new FakeSensitiveLogDataProtector(),
                logger);

            var result = await adapter.ProcessAsync(requestBody, "application/json", bot, null, 0, null, CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal((int)HttpStatusCode.OK, contentResult.StatusCode);
            Assert.Equal("text/plain", contentResult.ContentType);
            Assert.Equal("I challenge you to return this", contentResult.Content);
        }

        [Fact]
        public async Task ReturnsPlainTextContentResultWith200StatusForUnknownContentType()
        {
            var requestBody = JsonConvert.SerializeObject(new {
                type = "event_callback",
                @event = new {
                    type = "message",
                    text = "<@U01234567> here is <@U98765432>"
                },
                event_id = "EV000001"
            });
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var bot = Substitute.For<IBot>();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                new FakeEventQueueClient(),
                new FakeSensitiveLogDataProtector(),
                logger);

            var result = await adapter.ProcessAsync(requestBody, "badookie/ploukie", bot, null, 0, null, CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal((int)HttpStatusCode.OK, contentResult.StatusCode);
            Assert.Equal("text/plain", contentResult.ContentType);
            Assert.Equal("Unable to transform request / payload into Activity. Possible unrecognized request type",
                contentResult.Content);
        }

        [Fact]
        public async Task ReturnsPlainTextContentResultWith200StatusForUnrecognizedEventPayload()
        {
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var bot = Substitute.For<IBot>();
            var eventQueueClient = new FakeEventQueueClient();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                eventQueueClient,
                new FakeSensitiveLogDataProtector(),
                logger);

            var result = await adapter.ProcessAsync("{}", "plain/text", bot, null, 0, null, CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal((int)HttpStatusCode.OK, contentResult.StatusCode);
            Assert.Equal("text/plain", contentResult.ContentType);
            Assert.Equal("Unable to transform request / payload into Activity. Possible unrecognized request type",
                contentResult.Content);
        }

        [Fact]
        public async Task EnqueuesEventForProcessingAndReturnsContentResult()
        {
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var requestBody = JsonConvert.SerializeObject(new {
                team_id = "T0123457",
                type = "event_callback",
                @event = new {
                    type = "message",
                    text = "<@U01234567> here is <@U98765432>",
                    subtype = "file_share"
                },
                event_id = "EV000001"
            });
            var bot = Substitute.For<IBot>();
            var eventQueueClient = new FakeEventQueueClient();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                eventQueueClient,
                new FakeSensitiveLogDataProtector(),
                logger);

            var result = await adapter.ProcessAsync(requestBody, "application/json", bot, null, 0, null, CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(200, contentResult.StatusCode);
            Assert.Equal(requestBody, eventQueueClient.Enqueued.EventBody);
            var enqueuedMessage = Assert.IsType<EventEnvelope<FileShareMessageEvent>>(eventQueueClient.Enqueued.EventEnvelope);
            Assert.Equal("<@U01234567> here is <@U98765432>", enqueuedMessage.Event.Text);
        }

        [Fact]
        public async Task ThrowsExceptionWhenJsonTypeMismatch()
        {
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var requestBody = await EmbeddedResourceHelper.ReadSerializationResource("message.invalid-json.json");
            var bot = Substitute.For<IBot>();
            var eventQueueClient = new FakeEventQueueClient();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                eventQueueClient,
                new FakeSensitiveLogDataProtector(),
                logger);

            var exception = await Assert.ThrowsAsync<SlackEventDeserializationException>(
                () => adapter.ProcessAsync(requestBody, "application/json", bot, null, 0, null, CancellationToken.None));

            Assert.NotNull(exception.EventEnvelope);
            Assert.Equal("event_callback", exception.EventEnvelope.Type);
            Assert.Equal("Ev00000001", exception.EventEnvelope.EventId);
            Assert.Equal("T013108BYLS", exception.EventEnvelope.TeamId);
            Assert.Equal("C01A3DGTSP9", exception.EventEnvelope?.Event?.Channel);
            Assert.Equal("U012LKJFG0P", exception.EventEnvelope?.Event?.User);
        }

        [Fact]
        public async Task ThrowsExceptionWhenJsonIsGarbage()
        {
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var requestBody = "{garbage";
            var bot = Substitute.For<IBot>();
            var eventQueueClient = new FakeEventQueueClient();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                eventQueueClient,
                new FakeSensitiveLogDataProtector(),
                logger);

            var exception = await Assert.ThrowsAsync<SlackEventDeserializationException>(
                () => adapter.ProcessAsync(requestBody, "application/json", bot, null, 0, null, CancellationToken.None));

            Assert.Null(exception.EventEnvelope);
        }

        [Theory]
        [InlineData("bot_message", null)] // This should never happen, but we want to test it anyways.
        [InlineData("bot_message", "B0123457")]
        [InlineData("thread_subtype", "B0123457")]
        [InlineData("thread_broadcast", null)]
        [InlineData(null, "B0123457")] // This seems to be a Slack bug when Abbot sends a DM.
        public async Task IgnoresBotMessageOrMessageChangeRequestsOrThreadBroadcasts(string? subtype, string? botId)
        {
            var slackClient = Substitute.For<ISlackApiClient>();
            var logger = FakeLogger.Create<SlackAdapter>();
            var requestBody = JsonConvert.SerializeObject(new {
                team_id = "T0123457",
                type = "event_callback",
                @event = new {
                    type = "message",
                    subtype,
                    bot_id = botId,
                    text = "<@U01234567> here is <@U98765432>"
                },
                event_id = "EV000001"
            });
            var bot = Substitute.For<IBot>();
            var eventQueueClient = new FakeEventQueueClient();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                eventQueueClient,
                new FakeSensitiveLogDataProtector(),
                logger);

            var result = await adapter.ProcessAsync(requestBody, "application/json", bot, null, 0, null, CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal("Thanks, we got it.", contentResult.Content);
        }

        [Fact]
        public async Task LogsRateLimitedEvent()
        {
            var logger = FakeLogger.Create<SlackAdapter>();
            var requestBody = JsonConvert.SerializeObject(new {
                type = "app_rate_limited",
                minute_rate_limited = 1518467820,
                team_id = "T0123457"
            });
            var bot = Substitute.For<IBot>();
            var eventQueueClient = new FakeEventQueueClient();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                Substitute.For<ISlackApiClient>(),
                eventQueueClient,
                new FakeSensitiveLogDataProtector(),
                logger);

            var result = await adapter.ProcessAsync(
                requestBody,
                "application/json",
                bot,
                null,
                0,
                null,
                CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal("Ok, we'll deal with it.", contentResult.Content);
            var logged = logger.Provider.GetAllEvents().Last();
            Assert.Equal(LogLevel.Error, logged.LogLevel);
            Assert.Equal("Bot is being rate-limited for the team: 'T0123457'. Occurred on 2018-02-12T20:37:00.0000000+00:00.", logged.Message);
        }
    }

    public class TheDeleteActivityAsyncMethod
    {
        [Fact]
        public async Task CallsSlackApiToDeleteMessageWhenActivityIdProvided()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var reference = new ConversationReference
            {
                ActivityId = "1234567.890",
                ChannelId = "C0123456",
                Conversation = new ConversationAccount
                {
                    Properties = new JObject
                    {
                        ["ChannelData"] = JToken.FromObject(new DeleteChannelData(new SecretString("secret-api-token"), null))
                    }
                }
            };
            var turnContext = Substitute.For<ITurnContext>();
            var slackClient = Substitute.For<ISlackApiClient>();
            slackClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(new ApiResponse { Ok = true });
            var logger = FakeLogger.Create<SlackAdapter>();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                new FakeEventQueueClient(),
                new FakeSensitiveLogDataProtector(),
                logger);

            await adapter.DeleteActivityAsync(turnContext, reference, CancellationToken.None);

            await slackClient.Received().DeleteMessageAsync(
                "secret-api-token",
                "C0123456",
                "1234567.890");
        }

        [Fact]
        public async Task CallsSlackResponseUrlApiToDeleteMessageWhenResponseUrlProvided()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var reference = new ConversationReference
            {
                ChannelId = "C0123456",
                Conversation = new ConversationAccount
                {
                    Properties = new JObject
                    {
                        ["ChannelData"] = JToken.FromObject(new DeleteChannelData(new SecretString("secret-api-token"), new Uri("https://example.com/slack")))
                    }
                }
            };
            var turnContext = Substitute.For<ITurnContext>();
            var responseClient = Substitute.For<IResponseUrlClient>();
            responseClient.DeleteAsync("secret-api-token", Arg.Any<ResponseUrlDeleteMessageRequest>())
                .Returns(new ApiResponse { Ok = true });
            var slackClient = Substitute.For<ISlackApiClient>();
            slackClient.GetResponseUrlClient(new Uri("https://example.com/slack")).Returns(responseClient);
            var logger = FakeLogger.Create<SlackAdapter>();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                new FakeEventQueueClient(),
                new FakeSensitiveLogDataProtector(),
                logger);

            await adapter.DeleteActivityAsync(turnContext, reference, CancellationToken.None);

            await responseClient.Received().DeleteAsync(
                "secret-api-token", Arg.Any<ResponseUrlDeleteMessageRequest>());
        }
    }

    public class TheUpdateActivityAsyncMethod
    {
        [Fact]
        public async Task CallsSlackApiToUpdateMessageWhenActivityIdSupplied()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var channelData = new MessageChannelData(
                new SecretString("secret-api-token"),
                new MessageRequest { Timestamp = "1234.5678" },
                null);
            var activity = new Activity
            {
                Id = "1234567.890",
                ChannelData = channelData
            };
            var turnContext = Substitute.For<ITurnContext>();
            var slackClient = Substitute.For<ISlackApiClient>();
            MessageRequest? receivedMessageRequest = null;
            slackClient.UpdateMessageAsync(
                "secret-api-token",
                Arg.Do<MessageRequest>(m => receivedMessageRequest = m))
                .Returns(new MessageResponse { Ok = true });
            var logger = FakeLogger.Create<SlackAdapter>();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                new FakeEventQueueClient(),
                new FakeSensitiveLogDataProtector(),
                logger);

            var response = await adapter.UpdateActivityAsync(turnContext, activity, CancellationToken.None);

            Assert.Equal(activity.Id, response.Id);
            Assert.NotNull(receivedMessageRequest);
            Assert.Equal(channelData.Message.Timestamp, receivedMessageRequest.Timestamp);
        }

        [Fact]
        public async Task CallsSlackResponseUrlApiToUpdateMessageWhenResponseUrlSupplied()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var channelData = new MessageChannelData(
                new SecretString("secret-api-token"),
                new MessageRequest { Timestamp = "1234.5678", Blocks = new[] { new Section { Text = new MrkdwnText("hello") } } },
                new Uri("https://example.com/slack"));
            var activity = new Activity
            {
                ChannelData = channelData
            };
            var turnContext = Substitute.For<ITurnContext>();
            var slackClient = Substitute.For<ISlackApiClient>();
            var responseClient = Substitute.For<IResponseUrlClient>();
            slackClient.GetResponseUrlClient(new Uri("https://example.com/slack")).Returns(responseClient);
            ResponseUrlUpdateMessageRequest? receivedUpdateRequest = null;
            responseClient.UpdateAsync(
                    "secret-api-token",
                    Arg.Do<ResponseUrlUpdateMessageRequest>(m => receivedUpdateRequest = m))
                .Returns(new ApiResponse { Ok = true });
            var logger = FakeLogger.Create<SlackAdapter>();
            var adapter = new SlackAdapter(
                CreateSlackEventOptions(),
                new SlackEventDeduplicator(),
                slackClient,
                new FakeEventQueueClient(),
                new FakeSensitiveLogDataProtector(),
                logger);

            var response = await adapter.UpdateActivityAsync(turnContext, activity, CancellationToken.None);

            Assert.Equal(activity.Id, response.Id);
            Assert.NotNull(receivedUpdateRequest?.Blocks);
            var block = Assert.IsType<Section>(Assert.Single(receivedUpdateRequest.Blocks));
            Assert.NotNull(block.Text);
            Assert.Equal("hello", block.Text.Text);
        }
    }

    public class TheIsAllowedMessageEventMethod
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("channel_convert_to_private", true)]
        [InlineData("message_deleted", true)]
        [InlineData("channel_name", true)]
        [InlineData("file_share", true)]
        [InlineData("bot_message", false)]
        [InlineData("unknown", false)]
        public void ReturnsCorrectValueForSubtypes(string? subtype, bool expected)
        {
            var messageEvent = new MessageEvent
            {
                SubType = subtype
            };

            var result = SlackAdapter.IsAllowedMessageEvent(messageEvent);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReturnsFalseWhenSubtypeNullButBotIdNotEmpty()
        {
            var messageEvent = new MessageEvent
            {
                SubType = null,
                BotId = "B01234567"
            };

            var result = SlackAdapter.IsAllowedMessageEvent(messageEvent);

            Assert.False(result);
        }

        [Fact]
        public void ReturnsTrueForWorkflowBotMessages()
        {
            var messageEvent = new BotMessageEvent
            {
                SubType = "bot_message",
                BotProfile = new BotProfile(
                    Id: "B0123465",
                    Deleted: false,
                    Name: "Workflow Event",
                    Updated: "12345768",
                    AppId: "A0123465",
                    IsWorkflowBot: true,
                    TeamId: "T012345",
                    Icons: new BotIcons(null, null, null))
            };

            var result = SlackAdapter.IsAllowedMessageEvent(messageEvent);

            Assert.True(result);
        }
    }

    static IOptionsMonitor<SlackEventOptions> CreateSlackEventOptions(SlackEventOptions? options = null)
    {
        var sub = Substitute.For<IOptionsMonitor<SlackEventOptions>>();
        sub.CurrentValue.Returns(options ?? new());
        return sub;
    }

    public class TestEnvironment
    {
        public static TestEnvironment Create()
        {
            return new TestEnvironment();
        }

        TestEnvironment(
            SlackOptions? options = null,
            ISlackApiClient? slackApiClient = null,
            FakeEventQueueClient? eventQueue = null,
            FakeMemoryCache? cache = null)
        {
            Options = Microsoft.Extensions.Options.Options.Create(options ?? new());
            SlackApiClient = slackApiClient ?? Substitute.For<ISlackApiClient>();
            EventQueue = eventQueue ?? new FakeEventQueueClient();
            MemoryCache = cache ?? new FakeMemoryCache();
        }

        public IMemoryCache MemoryCache { get; set; }

        public IOptions<SlackOptions> Options { get; }

        public ISlackApiClient SlackApiClient { get; }

        public FakeEventQueueClient EventQueue { get; }
    }
}

using System.Diagnostics;
using Abbot.Common.TestHelpers;
using Newtonsoft.Json;
using NSubstitute;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Entities;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;
using Serious.TestHelpers;
using IBot = Microsoft.Bot.Builder.IBot;

public class SlackEventProcessorTests
{
    public class TheProcessEventMethod
    {
        [Fact]
        public async Task ProcessQueuedEventAndRetainsContentWhenSuccessful()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBot>(out var bot)
                .Build();

            var organization = env.TestData.Organization;
            var start = DateTime.UtcNow;
            var slackEvent = await env.CreateSlackMessageEventAsync(
                text: "This happened!",
                channel: "C000000123",
                eventId: "EV0000001",
                appId: organization.BotAppId);
            var performContext = new FakePerformContext();
            var processor = env.Activate<SlackEventProcessor>();

            await processor.ProcessEventAsync(
                slackEvent.Id,
                slackEvent.EventType,
                performContext,
                null,
                CancellationToken.None);

            await env.ReloadAsync(slackEvent);

            Assert.Null(slackEvent.Error);
            Assert.False(slackEvent.Content.Empty);
            Assert.True(slackEvent.Completed >= start);
            var receivedEvent = Assert.IsType<EventEnvelope<MessageEvent>>(env.BotFrameworkAdapter.ReceivedProcessedEvent.EventEnvelope);
            Assert.Equal("EV0000001", receivedEvent.EventId);
            Assert.Equal(organization.BotAppId, receivedEvent.ApiAppId);
            Assert.Equal(organization.PlatformId, receivedEvent.TeamId);
            Assert.Equal("This happened!", receivedEvent.Event.Text);
            Assert.Same(bot, env.BotFrameworkAdapter.ReceivedProcessedEvent.Bot);
        }

        [Fact]
        public async Task ThrowsExceptionWhenSlackEventDoesNotExist()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBot>()
                .Build();
            var performContext = new FakePerformContext();
            var processor = env.Activate<SlackEventProcessor>();

            await Assert.ThrowsAsync<UnreachableException>(
                () => processor.ProcessEventAsync(
                    new Id<SlackEvent>(1),
                    null,
                    performContext,
                    null,
                    CancellationToken.None));
        }

        [Fact]
        public async Task SetEventErrorWhenDeserializeReturnsEmptySecret()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBot>()
                .Build();
            var slackEvent = await env.CreateSlackEventAsync(eventType: "user_change", eventId: "EV0000001");
            var performContext = new FakePerformContext();
            var processor = env.Activate<SlackEventProcessor>();

            await processor.ProcessEventAsync(
                slackEvent.Id,
                "user_change",
                performContext,
                null,
                CancellationToken.None);

            await env.ReloadAsync(slackEvent);

            Assert.Equal("Could not deserialize the event envelope.", slackEvent.Error);
            Assert.Null(slackEvent.Completed);
        }

        [Fact]
        public async Task DoesNotSetEventErrorWhenDeserializeThrowsException()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBot>()
                .Build();

            var slackEvent = await env.CreateSlackEventAsync(eventContent: "{Not even JSON");
            var performContext = new FakePerformContext();
            var processor = env.Activate<SlackEventProcessor>();

            await processor.ProcessEventAsync(
                slackEvent.Id,
                slackEvent.EventType,
                performContext,
                null,
                CancellationToken.None);

            await env.ReloadAsync(slackEvent);

            Assert.Null(slackEvent.Error);
            Assert.Null(slackEvent.Completed);
            Assert.Equal("{Not even JSON", slackEvent.Content.Reveal());
        }

        [Fact]
        public async Task DoesNotSetEventErrorWhenProcessAsyncThrowsException()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IBot>()
                .Substitute<IBotFrameworkAdapter>(out var slackAdapter)
                .Build();
            var messageEvent = new EventEnvelope<AppMentionEvent>
            {
                Event = new AppMentionEvent
                {
                    Text = "This happened!"
                },
                TeamId = "T000001",
                EventId = "EV0000001",
            };
            var eventContent = JsonConvert.SerializeObject(messageEvent);
            var slackEvent = await env.CreateSlackEventAsync(messageEvent);
            slackAdapter.ProcessEventAsync(
                    Arg.Any<IEventEnvelope<EventBody>>(),
                    Arg.Any<IBot>(), null,
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new Exception("This happened!")));
            var performContext = new FakePerformContext();
            var processor = env.Activate<SlackEventProcessor>();

            await processor.ProcessEventAsync(
                slackEvent.Id,
                slackEvent.EventType,
                performContext,
                null,
                CancellationToken.None);

            await env.ReloadAsync(slackEvent);

            Assert.Null(slackEvent.Error);
            Assert.Null(slackEvent.Completed);
            Assert.Equal(eventContent, slackEvent.Content.Reveal());
        }

        [Fact]
        public async Task ThrowsOperationCancelledExceptionWhenCancellationTokenCancelled()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IBot>()
                .Build();
            var messageEvent = new EventEnvelope<AppMentionEvent>
            {
                Event = new AppMentionEvent
                {
                    Text = "This happened!"
                },
                TeamId = "T000001",
                EventId = "EV0000001",
            };
            var slackEvent = await env.CreateSlackEventAsync(messageEvent);
            var performContext = new FakePerformContext();
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            cancellationTokenSource.Cancel();
            var processor = env.Activate<SlackEventProcessor>();

            await Assert.ThrowsAsync<OperationCanceledException>(()
                => processor.ProcessEventAsync(
                    slackEvent.Id,
                    slackEvent.EventType,
                    performContext,
                    null,
                    cancellationToken));

            await env.ReloadAsync(slackEvent);

            Assert.Null(slackEvent.Error);
            Assert.Null(slackEvent.Completed);
        }
    }
}

using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Middleware;
using Serious.TestHelpers;

public class MessageFormatMiddlewareTests
{
    public class TheOnTurnAsyncMethod
    {
        [Fact]
        public async Task CallsNextDelegate()
        {
            var middleware = new MessageFormatMiddleware(new TestMessageFormatter());
            var nextDelegate = new FakeNextDelegateWrapper();
            var turnContext = new FakeTurnContext(new Activity
            {
                Text = "`hello`",
                ChannelId = "slack"
            });

            await middleware.OnTurnAsync(turnContext, nextDelegate.NextDelegate, CancellationToken.None);

            Assert.True(nextDelegate.Called);
        }

        [Fact]
        public async Task AppliesOverriddenDestinationConversationFromAbbotMetadataIfPresent()
        {
            var testFormatter = new TestMessageFormatter();
            var middleware = new MessageFormatMiddleware(testFormatter);
            var overridenMessageTarget = new MessageTarget(new ChatAddress(ChatAddressType.Room, "C0123431"));
            var incomingActivity = new Activity
            {
                Text = "`hello`",
                ChannelId = "slack",
                Conversation = new(),
            };

            var nextDelegate = new FakeNextDelegateWrapper();
            var turnContext = new FakeTurnContext(incomingActivity);
            await middleware.OnTurnAsync(turnContext, nextDelegate.NextDelegate, CancellationToken.None);

            var originalChannelData = new object();
            var outgoingActivity = new Activity
            {
                Text = "`howdy`",
                ChannelId = "slack",
                Conversation = new(),
                ChannelData = originalChannelData,
            };
            outgoingActivity.OverrideDestination(overridenMessageTarget);
            Assert.NotSame(originalChannelData, outgoingActivity.ChannelData);
            Assert.NotSame(overridenMessageTarget, outgoingActivity.Conversation);

            await turnContext.SendActivityAsync(outgoingActivity);

            Assert.NotNull(testFormatter.ReceivedActivity);
            Assert.Equal("::C0123431", testFormatter.ReceivedActivity.Conversation.Id);
            Assert.Same(originalChannelData, testFormatter.ReceivedActivity.ChannelData);
        }

        class TestMessageFormatter : IMessageFormatter
        {
            public Activity? ReceivedActivity { get; private set; }

            public void FormatOutgoingMessage(Activity activity, ITurnContext turnContext)
            {
                ReceivedActivity = activity;
                activity.Text += $" (formatted by {nameof(TestMessageFormatter)})";
            }

            public void NormalizeIncomingMessage(Activity activity)
            {
            }
        }
    }
}

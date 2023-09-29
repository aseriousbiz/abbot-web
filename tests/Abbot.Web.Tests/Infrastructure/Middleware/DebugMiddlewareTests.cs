using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Serious.Abbot.Infrastructure.Middleware;
using Serious.TestHelpers;
using Xunit;

public class DebugMiddlewareTests
{
    public class TheOnTurnAsyncMethod
    {
        [Fact]
        public async Task IgnoresNonDebugMessagesAndCallsNext()
        {
            var middleware = new DebugMiddleware();
            var nextDelegate = new FakeNextDelegateWrapper();
            var turnContext = new FakeTurnContext(new Activity { Text = "@abbot echo test" });

            await middleware.OnTurnAsync(turnContext, nextDelegate.NextDelegate);

            Assert.True(nextDelegate.Called);
            Assert.Equal("@abbot echo test", turnContext.Activity.Text);
        }

        [Theory]
        [InlineData("—debug")]
        [InlineData("--debug")]
        public async Task StripsDebugFlagWhenTurnStateHasDebugFlag(string debugSuffix)
        {
            var middleware = new DebugMiddleware();
            var nextDelegate = new FakeNextDelegateWrapper();
            var turnContext = new FakeTurnContext(new Activity
            {
                Text = $"random text {debugSuffix}",
                From = new ChannelAccount("blah:blah"),
                Recipient = new ChannelAccount("stuff:stuff")
            })
            {
                TurnState = { { "DebugMiddlewareFlag", "true" } } // Signals that we want debug output
            };

            await middleware.OnTurnAsync(turnContext, nextDelegate.NextDelegate);

            Assert.Equal("random text", turnContext.Activity.Text);
        }

        [Fact]
        public async Task SendsDebugOutputAppendedToOutgoingMessage()
        {
            var middleware = new DebugMiddleware();
            var nextDelegate = new FakeNextDelegateWrapper();
            var turnContext = new FakeTurnContext(new Activity
            {
                Text = "random text --debug",
                From = new ChannelAccount("from:from"),
                Recipient = new ChannelAccount("stuff:stuff")
            })
            {
                TurnState = { { "DebugMiddlewareFlag", "true" } } // Signals that we want debug output
            };

            await middleware.OnTurnAsync(turnContext, nextDelegate.NextDelegate);

            var sentActivity = new Activity
            {
                Text = "Some Text"
            };
            await turnContext.SendActivityAsync(sentActivity);
            Assert.Contains(@"Some Text

```
Abbot Debug Information (Sent)", sentActivity.Text);
        }
    }
}

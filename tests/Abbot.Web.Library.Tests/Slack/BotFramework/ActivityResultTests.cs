using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Newtonsoft.Json;
using NSubstitute;
using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.TestHelpers;
using Xunit;

public class ActivityResultTests
{
    public class TheExecuteResultAsyncMethod
    {
        [Fact]
        public async Task RunsThePipeline()
        {
            var incomingTurnContext = Substitute.For<ITurnContext>();
            incomingTurnContext.TurnState.Returns(new TurnContextStateCollection
            {
                ["httpBody"] = "The body of the HTTP response can be controlled in this way."
            });
            ITurnContext? receivedTurnContext = null;
            Task RunPipelineAsync(ITurnContext turnContext)
            {
                receivedTurnContext = turnContext;
                return Task.CompletedTask;
            }
            var activityResult = new ActivityResult(incomingTurnContext, RunPipelineAsync);
            var actionContext = new FakeActionContext();

            await activityResult.ExecuteResultAsync(actionContext);

            Assert.NotNull(receivedTurnContext);
            Assert.Same(activityResult.TurnContext, receivedTurnContext);
            var responseBody = await actionContext.GetResponseBodyAsync();
            Assert.Equal("The body of the HTTP response can be controlled in this way.", responseBody);
        }

        [Fact]
        public async Task RespondsWithErrorsResponseActions()
        {
            var incomingTurnContext = Substitute.For<ITurnContext>();
            incomingTurnContext.TurnState.Returns(new TurnContextStateCollection
            {
                [ActivityResult.ResponseBodyKey] = new ErrorResponseAction(new Dictionary<string, string>
                {
                    ["block_1"] = "Wrong",
                    ["block_2"] = "Just wrong"
                })
            });
            Task RunPipelineAsync(ITurnContext turnContext)
            {
                return Task.CompletedTask;
            }
            var activityResult = new ActivityResult(incomingTurnContext, RunPipelineAsync);
            var actionContext = new FakeActionContext();

            await activityResult.ExecuteResultAsync(actionContext);

            Assert.Equal("application/json", actionContext.HttpContext.Response.ContentType);
            var responseBody = await actionContext.GetResponseBodyAsync();
            Assert.Equal("{\"errors\":{\"block_1\":\"Wrong\",\"block_2\":\"Just wrong\"},\"response_action\":\"errors\"}", responseBody);
        }
    }
}

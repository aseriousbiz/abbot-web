using System;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serious.Abbot.Controllers;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Scripting;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack.AspNetCore;
using Serious.Slack.BotFramework;
using Serious.TestHelpers;
using Xunit;
using IBot = Microsoft.Bot.Builder.IBot;

public class SlackBotControllerTests : ControllerTestBase<SlackBotController>
{
    public class ThePostAsyncMethod : SlackBotControllerTests
    {
        [Fact]
        public void HasVerifySlackRequestAttributeApplied()
        {
            var method = typeof(SlackBotController).GetMethod(nameof(SlackBotController.PostAsync));

            Assert.NotNull(method);
            Assert.True(method.GetCustomAttributesData()
                .Any(a => a.AttributeType == typeof(VerifySlackRequestAttribute)));
        }

        [Fact]
        public async Task ThrowsWithoutSlackRequestBody()
        {
            Builder.Substitute<IBot>();

            await InvokeControllerAsync(async controller => {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    controller.PostAsync(null));

                Assert.Equal(
                    "Slack Request Body is not stored in HttpContext. This means the VerifySlackRequest attribute is not applied or working correctly.",
                    ex.Message);
            });
        }

        [Fact]
        public async Task ThrowsWithoutContentType()
        {
            Builder.Substitute<IBot>();

            await InvokeControllerAsync(async controller => {
                SetSlackRequestBody(controller);
                Assert.Null(controller.Request.ContentType);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    controller.PostAsync(null));

                Assert.Equal("Slack sent us a request without a Content-Type header.", ex.Message);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData(42)]
        public async Task DoesNotLogIfSlackRetryNumHeaderNotPresent(int? integrationId)
        {
            Builder
                .Substitute<IBot>(out var bot)
                .Substitute<IBotFrameworkAdapter>(out var botFrameworkAdapter);

            var body = """{"body":true}""";
            var contentType = "application/json";
            var expectedResult = new NoContentResult();

            botFrameworkAdapter.ProcessAsync(body, contentType, bot, integrationId, 0, null, default)
                .Returns(expectedResult);

            await InvokeControllerAsync(async controller => {
                SetSlackRequestBody(controller, body);
                controller.Request.ContentType = contentType;

                var result = await controller.PostAsync(integrationId);

                Assert.Same(expectedResult, result);

                Assert.Empty(Env.GetAllLogs<SlackBotController>(LogLevel.Information));
            });
        }

        [Theory]
        [InlineData("oops", "Received Slack retry number 1 for request ts=oops. Delay = (null)ms. Reason: http_error", "SlackRetry", LogLevel.Information, 1)]
        [InlineData("1672531195", "Received Slack retry number 2 for request ts=1672531195. Delay = 5000ms. Reason: http_error", "SlackRetry", LogLevel.Information, 2)]
        [InlineData("1672531195", "Received FINAL Slack retry number 3 for request ts=1672531195. Delay = 5000ms. Reason: http_error", "SlackFinalRetry", LogLevel.Warning, 3)]
        public async Task LogsIfSlackSendsRetryNumHeader(string timestamp, string expectedLogMessage, string expectedEvent, LogLevel expectedLevel, int retryCount)
        {
            Builder
                .Substitute<IBot>(out var bot)
                .Substitute<IBotFrameworkAdapter>(out var botFrameworkAdapter);

            Env.Clock.TravelTo(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc)); // Happy New Year!

            var body = """{"body":true}""";
            var contentType = "application/json";
            var integrationId = 42;
            var expectedResult = new NoContentResult();

            botFrameworkAdapter.ProcessAsync(body, contentType, bot, integrationId, retryCount, "http_error", default)
                .Returns(expectedResult);

            await InvokeControllerAsync(async controller => {
                SetSlackRequestBody(controller, body);
                controller.Request.ContentType = contentType;

                controller.Request.Headers["X-Slack-Retry-Num"] = $"{retryCount}";
                controller.Request.Headers["X-Slack-Request-Timestamp"] = timestamp;
                controller.Request.Headers["X-Slack-Retry-Reason"] = "http_error";

                var result = await controller.PostAsync(integrationId);

                Assert.Same(expectedResult, result);

                var log = Env.GetAllLogs<SlackBotController>(eventName: expectedEvent).Last();
                Assert.Equal(expectedLogMessage, log.Message);
                Assert.Equal(expectedLevel, log.LogLevel);
            });
        }

        void SetSlackRequestBody(ControllerBase controller, string body = "{}") =>
            controller.HttpContext.Items[VerifySlackRequestAttribute.RequestBodyKey] = body;
    }
}

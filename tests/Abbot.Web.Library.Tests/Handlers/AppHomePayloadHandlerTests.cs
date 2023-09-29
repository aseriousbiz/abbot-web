using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.Payloads;
using Xunit;

public class AppHomePayloadHandlerTests
{
    public class TheOnPlatformEventAsyncMethod
    {
        [Theory]
        [InlineData(null, null, null, "@abbot", "Abbot")]
        [InlineData(null, "test-abbot", null, "@test-abbot", "Abbot")]
        [InlineData("U03DYLAKR6U", "Bot Name", "Test Abbot", "<@U03DYLAKR6U>", "Test Abbot")]
        public async Task WithAppHomeOpenedEventEnsuresFromMemberAndSendsOpenerDirectMessageWithEmailPromptWhenMemberHasNoEmail(string? botUserId, string botName, string? appName, string expectedBotMention, string expectedAppName)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISkillRouter>(out var skillRouter)
                .Substitute<IPayloadHandlerInvoker>(out var handlerInvoker)
                .Build();

            var org = env.TestData.Organization;
            org.PlatformBotUserId = botUserId;
            org.BotName = botName;
            org.BotAppName = appName;
            env.Db.Update(org);

            var member = env.TestData.Member;
            var user = member.User;
            user.Email = null;
            await env.Db.SaveChangesAsync();

            var appHomeOpenedEvent = env.CreateFakePlatformEvent(new AppHomeOpenedEvent
            {
                Tab = "Home",
                User = user.PlatformUserId
            });
            skillRouter.RetrievePayloadHandler(appHomeOpenedEvent)
                .Returns(new PayloadHandlerRouteResult(handlerInvoker));
            var handler = env.Activate<AppHomePayloadHandler>();

            await handler.OnPlatformEventAsync(appHomeOpenedEvent);

            var sentMessage = Assert.Single(env.Responder.SentMessages);
            Assert.Equal(
                $":wave: Welcome to {expectedAppName}! Please let me know your email by replying `{expectedBotMention} my email is {{email}}` in this DM channel. This way we can get in touch if we have any important questions or updates for you. Have a great day!",
                sentMessage.Text);
            Assert.Null(member.User.Email);
            Assert.True(member.Welcomed);
        }

        [Fact]
        public async Task WithDisabledOrganizationShowsMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.Enabled = false;
            await env.Db.SaveChangesAsync();
            var handler = env.Activate<AppHomePayloadHandler>();
            var payload = new AppHomeOpenedEvent();
            var platformEvent = env.CreateFakePlatformEvent(payload);

            await handler.OnPlatformEventAsync(platformEvent);

            var publishedAppHome = Assert.Single(env.SlackApi.PostedAppHomes);
            var section = Assert.IsType<Section>(Assert.Single(publishedAppHome.View.Blocks));
            Assert.NotNull(section.Text);
            Assert.Equal($"Sorry, I cannot do that. Your organization is disabled. Please contact {WebConstants.SupportEmail} for more information.", section.Text.Text);
        }
    }
}

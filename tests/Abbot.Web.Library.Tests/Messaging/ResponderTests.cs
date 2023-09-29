using System.Collections.Generic;
using NSubstitute;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messaging;
using Serious.Cryptography;
using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.Slack.BotFramework.Model;
using Serious.TestHelpers;

public class ResponderTests
{
    private static SlackBotChannelUser CreateTestBotChannelUser(string scopes = "chat:write.customize") =>
        new("T001", "B001", "U001", "The-Abbot",
            botResponseAvatar: "https://foo.example.com/cool-bot/",
            apiToken: new SecretString("secret-api-token", new FakeDataProtectionProvider()),
            scopes: scopes);

    public class TheOpenModalViewAsyncMethod
    {
        [Fact]
        public async Task CallsSlackApiClient()
        {
            var turnContext = new FakeTurnContext();
            turnContext.SetApiToken(new SecretString("secret-api-token", new FakeDataProtectionProvider()));
            var bot = CreateTestBotChannelUser();
            var view = new ViewUpdatePayload();
            var slackApiClient = Substitute.For<ISlackApiClient>();
            slackApiClient
                .OpenModalViewAsync("secret-api-token", new OpenViewRequest("TRIGGER_ID", view))
                .Returns(new ViewResponse { Ok = true });

            var responder = new Responder(slackApiClient, turnContext, bot);

            await responder.OpenModalAsync("TRIGGER_ID", view);
        }
    }

    public class ThePushModalViewAsyncMethod
    {
        [Fact]
        public async Task CallsSlackApiClient()
        {
            var turnContext = new FakeTurnContext();
            turnContext.SetApiToken(new SecretString("secret-api-token", new FakeDataProtectionProvider()));
            var bot = CreateTestBotChannelUser();
            var view = new ViewUpdatePayload();
            var slackApiClient = Substitute.For<ISlackApiClient>();
            slackApiClient
                .PushModalViewAsync("secret-api-token", new OpenViewRequest("TRIGGER_ID", view))
                .Returns(new ViewResponse { Ok = true });

            var responder = new Responder(slackApiClient, turnContext, bot);

            await responder.PushModalAsync("TRIGGER_ID", view);
        }
    }

    public class TheUpdateModalViewAsyncMethod
    {
        [Fact]
        public async Task CallsSlackApiClient()
        {
            var turnContext = new FakeTurnContext();
            turnContext.SetApiToken(new SecretString("secret-api-token", new FakeDataProtectionProvider()));
            var bot = CreateTestBotChannelUser();
            var view = new ViewUpdatePayload();
            var slackApiClient = Substitute.For<ISlackApiClient>();
            slackApiClient
                .UpdateModalViewAsync("secret-api-token", new UpdateViewRequest("VIEW_ID", null, view))
                .Returns(new ViewResponse { Ok = true });

            var responder = new Responder(slackApiClient, turnContext, bot);

            await responder.UpdateModalAsync("VIEW_ID", view);
        }
    }

    public class TheDeleteActivityAsyncMethod
    {
        [Fact]
        public async Task UnwrapsApiTokenAndPassesItInConversationReference()
        {
            var turnContext = new FakeTurnContext();
            var bot = CreateTestBotChannelUser();
            var responder = new Responder(new FakeSimpleSlackApiClient(), turnContext, bot);

            await responder.DeleteActivityAsync("C0123456", "12343411.12342");

            var conversationReference = turnContext.DeletedConversationReference;
            Assert.NotNull(conversationReference);
            Assert.Equal("12343411.12342", conversationReference.ActivityId);
            Assert.Equal("C0123456", conversationReference.ChannelId);
            var channelDataToken = conversationReference.Conversation.Properties["ChannelData"];
            Assert.NotNull(channelDataToken);
            var channelData = channelDataToken.ToObject<DeleteChannelData>();
            Assert.NotNull(channelData);
            Assert.Equal("secret-api-token", channelData.ApiToken.Reveal());
        }
    }

    public class TheReportValidationErrorsMethod
    {
        [Fact]
        public void IgnoresEmptyDictionary()
        {
            var turnContext = new FakeTurnContext();
            var bot = CreateTestBotChannelUser();
            var responder = new Responder(new FakeSimpleSlackApiClient(), turnContext, bot);

            responder.ReportValidationErrors(new Dictionary<string, string>());

            Assert.False(responder.HasValidationErrors);
        }

        [Fact]
        public void AddsValidationErrorsToTurnState()
        {
            var turnContext = new FakeTurnContext();
            var bot = CreateTestBotChannelUser();
            var responder = new Responder(new FakeSimpleSlackApiClient(), turnContext, bot);

            responder.ReportValidationErrors(new Dictionary<string, string>
            {
                ["block-1"] = "something bad happened 1.",
                ["block-2"] = "something bad happened 2."
            });

            Assert.True(responder.HasValidationErrors);
            var validationErrors = Assert.IsType<ErrorResponseAction>(turnContext.TurnState[ActivityResult.ResponseBodyKey]);
            var errors = validationErrors.Errors;
            Assert.Equal(2, errors.Count);
            Assert.Equal(errors["block-1"], "something bad happened 1.");
            Assert.Equal(errors["block-2"], "something bad happened 2.");
        }

        [Fact]
        public void MergesValidationErrorsToTurnState()
        {
            var turnContext = new FakeTurnContext();
            var bot = CreateTestBotChannelUser();
            var responder = new Responder(new FakeSimpleSlackApiClient(), turnContext, bot);
            responder.ReportValidationErrors(new Dictionary<string, string>
            {

                ["block-1"] = "something bad happened 1.",
                ["block-2"] = "something bad happened 2."
            });

            responder.ReportValidationErrors(new Dictionary<string, string>
            {

                ["block-2"] = "something else bad happened.",
                ["block-3"] = "something bad happened 3."
            });

            Assert.True(responder.HasValidationErrors);
            var validationErrors = Assert.IsType<ErrorResponseAction>(turnContext.TurnState[ActivityResult.ResponseBodyKey]);
            var errors = validationErrors.Errors;
            Assert.Equal(3, errors.Count);
            Assert.Equal(errors["block-1"], "something bad happened 1.");
            Assert.Equal(errors["block-2"], "something bad happened 2. something else bad happened.");
            Assert.Equal(errors["block-3"], "something bad happened 3.");
        }
    }
}

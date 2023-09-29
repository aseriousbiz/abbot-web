using Microsoft.Bot.Builder.Adapters.Slack;
using Microsoft.Bot.Schema;
using Serious.Slack;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Xunit;

public class SlackHelperTests
{
    public class TheEventToActivityMethod
    {
        [Fact]
        public void SetsActivityTypeAndActionForUninstallEvent()
        {
            var uninstallEvent = new EventEnvelope<AppUninstalledEvent>
            {
                TeamId = "T12345678",
                Token = "whatever",
                ApiAppId = "A12345678",
                EventId = "Ev12345678",
                EventTime = 123456678,
                Event = new AppUninstalledEvent { EventTimestamp = "1469470591.759709" }
            };

            var activity = SlackHelper.EventToActivity(uninstallEvent);

            Assert.Equal("remove", activity.Action);
            Assert.Equal(ActivityTypes.InstallationUpdate, activity.Type);
        }

        [Theory]
        [InlineData(null, ActivityTypes.Message)]
        [InlineData("file_upload", ActivityTypes.Event)]
        [InlineData("channel_convert_to_private", ActivityTypes.Event)]
        public void SetsActivityTypeAndActionForMessage(string subtype, string activityType)
        {
            var messageEvent = new EventEnvelope<MessageEvent>
            {
                TeamId = "T12345678",
                Token = "whatever",
                ApiAppId = "A12345678",
                EventId = "Ev12345678",
                EventTime = 123456678,
                Event = new MessageEvent { SubType = subtype }
            };

            var activity = SlackHelper.EventToActivity(messageEvent);

            Assert.Equal(activityType, activity.Type);
        }

        [Fact]
        public void SetsActivityTypeToEventForSlackBotMessage()
        {
            var messageEvent = new EventEnvelope<MessageEvent>
            {
                TeamId = "T12345678",
                Token = "whatever",
                ApiAppId = "A12345678",
                EventId = "Ev12345678",
                EventTime = 123456678,
                Event = new MessageEvent { User = "USLACKBOT", EventTimestamp = "1469470591.759709" }
            };

            var activity = SlackHelper.EventToActivity(messageEvent);

            Assert.Null(activity.Action);
            Assert.Equal(ActivityTypes.Event, activity.Type);
        }

        [Fact]
        public void SetsActivityTypeAsMessageForBotWorkflowMessage()
        {
            var botMessageEvent = new EventEnvelope<BotMessageEvent>
            {
                TeamId = "T12345678",
                Token = "whatever",
                ApiAppId = "A12345678",
                EventId = "Ev12345678",
                EventTime = 123456678,
                Event = new BotMessageEvent
                {
                    Channel = "C0123434",
                    Text = "Workflow bot says what up?",
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
                }
            };

            var activity = SlackHelper.EventToActivity(botMessageEvent);

            Assert.Equal(ActivityTypes.Message, activity.Type);
            Assert.Equal("C0123434", activity.Conversation.Id);
            Assert.Equal("Workflow bot says what up?", activity.Text);
        }

        [Fact]
        public void SetsActivityTypeAndActionForEvent()
        {
            var userChangeEvent = new EventEnvelope<UserChangeEvent>
            {
                TeamId = "T12345678",
                Token = "whatever",
                ApiAppId = "A12345678",
                EventId = "Ev12345678",
                EventTime = 123456678,
                Event = new UserChangeEvent { EventTimestamp = "1469470591.759709" }
            };

            var activity = SlackHelper.EventToActivity(userChangeEvent);

            Assert.Null(activity.Action);
            Assert.Equal(ActivityTypes.Event, activity.Type);
        }

        [Fact]
        public void SetsChannelForMessageReactionEvent()
        {
            var messageReactionEvent = new EventEnvelope<ReactionAddedEvent>
            {
                TeamId = "T12345678",
                Token = "whatever",
                ApiAppId = "A12345678",
                EventId = "Ev12345678",
                EventTime = 123456678,
                Event = new ReactionAddedEvent
                {
                    Reaction = "+1",
                    Item = new ReactionItem("message", "C0123456", "123456.734823")
                }
            };

            var activity = SlackHelper.EventToActivity(messageReactionEvent);

            Assert.Null(activity.Action);
            Assert.Equal(ActivityTypes.Event, activity.Type);
            Assert.Equal("C0123456", activity.Conversation.Id);
        }
    }

    public class ThePayloadToActivityMethod
    {
        [Theory]
        [InlineData(null, "::C001234")]
        [InlineData("1234567890.123456", "::C001234:1234567890.123456")]
        public void CreatesMessageActivityFromInteractiveMessagePayload(string threadTimestamp, string expectedConversationId)
        {
            var payload = new InteractiveMessagePayload
            {
                User = new UserIdentifier
                {
                    Id = "U1234"
                },
                Team = new TeamIdentifier
                {
                    Id = "T01234"
                },
                Channel = new ChannelInfo
                {
                    Id = "C001234"
                },
                OriginalMessage = new()
                {
                    ThreadTimestamp = threadTimestamp
                }
            };

            var activity = SlackHelper.PayloadToActivity(payload);

            Assert.Equal("message", activity.Type);
            Assert.Equal(expectedConversationId, activity.Conversation.Id);
            Assert.Equal("https://slack.botframework.com/", activity.ServiceUrl);
            Assert.Same(payload, activity.ChannelData);
        }

        [Theory]
        [InlineData(null, "::C001234")]
        [InlineData("1234567890.123456", "::C001234:1234567890.123456")]
        public void CreatesMessageActivityFromMessageBlockActionsPayload(string threadTimestamp, string expectedConversationId)
        {
            var payload = new MessageBlockActionsPayload
            {
                User = new UserIdentifier
                {
                    Id = "U1234"
                },
                Team = new TeamIdentifier
                {
                    Id = "T01234"
                },
                Channel = new ChannelInfo
                {
                    Id = "C001234"
                },
                Message = new SlackMessage
                {
                    ThreadTimestamp = threadTimestamp
                }
            };

            var activity = SlackHelper.PayloadToActivity(payload);

            Assert.Equal("message", activity.Type);
            Assert.Equal(expectedConversationId, activity.Conversation.Id);
            Assert.Equal("https://slack.botframework.com/", activity.ServiceUrl);
            Assert.Same(payload, activity.ChannelData);
        }

        [Fact]
        public void CreatesEventActivityFromViewBlockActionsPayload()
        {
            var payload = new ViewBlockActionsPayload
            {
                User = new UserIdentifier
                {
                    Id = "U1234"
                },
                Team = new TeamIdentifier
                {
                    Id = "T01234"
                },
                View = new ModalView
                {
                    Id = "V012345"
                },
                Container = new ViewContainer("V012345")
            };

            var activity = SlackHelper.PayloadToActivity(payload);

            Assert.Equal("event", activity.Type);
            Assert.Same(payload, activity.ChannelData);
        }
    }
}

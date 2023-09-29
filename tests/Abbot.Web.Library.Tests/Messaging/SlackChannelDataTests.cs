using Newtonsoft.Json.Linq;
using Serious.Abbot.Messaging;
using Serious.Slack.Events;
using Xunit;

public class SlackChannelDataTests
{
    public class DeserializesProperly
    {
        [Fact]
        public void FromBotAddedEvent()
        {
            var channelData = JObject.FromObject(new {
                SlackMessage = new {
                    type = "bot_added",
                    bot = new {
                        id = "B00001",
                        Name = "the-abbot",
                        app_id = "A4H1JB4AZ"
                    }
                }
            });

            var slackChannelData = channelData.ToObject<SlackChannelData>();

            Assert.NotNull(slackChannelData);
            var botAddedEvent = Assert.IsType<BotAddedEvent>(slackChannelData.SlackMessage);
            Assert.Equal("B00001", botAddedEvent.Bot.Id);
            Assert.Equal("the-abbot", botAddedEvent.Bot.Name);
            Assert.Equal("A4H1JB4AZ", botAddedEvent.Bot.AppId);
        }

        [Theory]
        [InlineData("app_mention")]
        [InlineData("message")]
        public void FromMessageEvent(string eventType)
        {
            var channelData = JObject.FromObject(new {
                SlackMessage = new {
                    type = "event_callback",
                    token = "TOKEN123",
                    team_id = "T12345",
                    api_app_id = "A12345",
                    @event = new {
                        type = eventType,
                        user = "U12345",
                        text = "Hello World",
                        ts = "12345.67890",
                        channel = "C12345",
                        channel_type = "channel",
                        blocks = new[]
                        {
                            new
                            {
                                type = "rich_text",
                                block_id = "block-1",
                                elements = new[]
                                {
                                    new
                                    {
                                        type = "rich_text_section",
                                        elements = new[]
                                        {
                                            new
                                            {
                                                type = "text",
                                                text = "Hello World"
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        team = "T12345",
                        event_ts = "12345.67890"
                    },
                    event_id = "Ev12345",
                    event_time = 1234567890
                },
                ApiToken = "ApiToken123"
            });

            var slackChannelData = channelData.ToObject<SlackChannelData>();

            Assert.NotNull(slackChannelData);
            var messageEvent = Assert.IsAssignableFrom<IEventEnvelope<MessageEvent>>(slackChannelData.SlackMessage);
            Assert.Equal("TOKEN123", messageEvent.Token);
            Assert.Equal("T12345", messageEvent.TeamId);
            Assert.Equal("A12345", messageEvent.ApiAppId);
            Assert.Equal("Ev12345", messageEvent.EventId);
            Assert.Equal(1234567890, messageEvent.EventTime);
            Assert.Equal("ApiToken123", slackChannelData.ApiToken);
            var message = messageEvent.Event;
            Assert.Equal(eventType, message.Type);
            Assert.Equal("U12345", message.User);
            Assert.Equal("Hello World", message.Text);
            Assert.Equal("12345.67890", message.Timestamp);
            Assert.Equal("C12345", message.Channel);
            Assert.Equal("channel", message.ChannelType);
            Assert.Equal("T12345", message.Team);
            Assert.Equal("12345.67890", message.EventTimestamp);
            // TODO (@haacked): deserialize the blocks and include them in the test.
        }

        [Fact]
        public void FromAppHomeOpenedEvent()
        {
            var channelData = JObject.FromObject(new {
                SlackMessage = new {
                    type = "event_callback",
                    token = "TOKEN123",
                    team_id = "T12345",
                    api_app_id = "A12345",
                    @event = new {
                        type = "app_home_opened",
                        tab = "home"
                    }
                }
            });

            var slackChannelData = channelData.ToObject<SlackChannelData>();

            Assert.NotNull(slackChannelData);
            var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(slackChannelData.SlackMessage);
            Assert.Equal("TOKEN123", envelope.Token);
            Assert.Equal("T12345", envelope.TeamId);
            Assert.Equal("A12345", envelope.ApiAppId);
            var appHomeOpenedEvent = envelope.Event;
            Assert.Equal("home", appHomeOpenedEvent.Tab);
        }

        [Fact]
        public void FromUserChangedEvent()
        {
            var channelData = JObject.FromObject(new {
                SlackMessage = new {
                    type = "event_callback",
                    token = "TOKEN123",
                    team_id = "T12345",
                    api_app_id = "A12345",
                    @event = new {
                        type = "user_change",
                        user = new {
                            id = "U6A0HFBS4",
                            team_id = "T025BJED9",
                            name = "dice",
                            profile = new {
                                display_name = "the dice"
                            }
                        }
                    }
                }
            });

            var slackChannelData = channelData.ToObject<SlackChannelData>();

            Assert.NotNull(slackChannelData);
            var userChangeEvent = Assert.IsType<EventEnvelope<UserChangeEvent>>(slackChannelData.SlackMessage);
            Assert.Equal("the dice", userChangeEvent.Event.User.Profile.DisplayName);
        }

    }
}

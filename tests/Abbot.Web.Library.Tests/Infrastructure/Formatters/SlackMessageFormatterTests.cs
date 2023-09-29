using System;
using Microsoft.Bot.Schema;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Cryptography;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Payloads;
using Serious.TestHelpers;

public class SlackMessageFormatterTests
{
    public class TheFormatOutgoingMessageMethod
    {
        [Fact]
        public void IgnoresNonMessages()
        {
            var activity = new Activity
            {
                Text = "[Abbot](https://ab.bot/)",
                Type = "non-message",
                TextFormat = "markdown"
            };
            var turnContext = new FakeTurnContext(activity);
            var formatter = new SlackMessageFormatter();

            formatter.FormatOutgoingMessage(activity, turnContext);

            Assert.Null(activity.ChannelData);
            Assert.Equal("[Abbot](https://ab.bot/)", activity.Text);
        }

        [Fact]
        public void IgnoresNonMarkdown()
        {
            var activity = new Activity
            {
                Text = "[Abbot](https://ab.bot/)",
                Type = "message",
                TextFormat = "non-markdown"
            };
            var turnContext = new FakeTurnContext(activity);
            var formatter = new SlackMessageFormatter();

            formatter.FormatOutgoingMessage(activity, turnContext);

            Assert.Null(activity.ChannelData);
            Assert.Equal("[Abbot](https://ab.bot/)", activity.Text);
        }

        [Fact]
        public void ThrowsExceptionWhenApiTokenNotSet()
        {
            var activity = new Activity
            {
                Text = "some text",
                Type = "message",
                TextFormat = "markdown",
                Conversation = new() { Id = new SlackConversationId("C", "123").ToString() },
            };
            var turnContext = new FakeTurnContext(activity);
            var formatter = new SlackMessageFormatter();

            var exception = Assert.Throws<InvalidOperationException>(() => formatter.FormatOutgoingMessage(activity, turnContext));

            Assert.Equal("No Slack API Token provided", exception.Message);
        }

        [Theory]
        [InlineData("markdown")]
        [InlineData("")]
        [InlineData(null)]
        public void SetsChannelDataWithMessageAndApiTokenForMarkdownOrEmptyFormat(string textFormat)
        {
            var activity = new Activity
            {
                Text = "some text",
                Type = "message",
                TextFormat = textFormat,
                Conversation = new() { Id = new SlackConversationId("C", "123").ToString() },
            };
            var turnContext = new FakeTurnContext(activity);
            turnContext.SetApiToken(new SecretString("xoxb-whatever-makes-the-test-pass", new FakeDataProtectionProvider()));
            var formatter = new SlackMessageFormatter();

            formatter.FormatOutgoingMessage(activity, turnContext);

            var channelData = Assert.IsType<MessageChannelData>(activity.ChannelData);
            Assert.Equal("some text", channelData.Message.Text);
            Assert.Equal("xoxb-whatever-makes-the-test-pass", channelData.ApiToken.Reveal());
            Assert.Null(activity.Text);
        }

        [Fact]
        public void IgnoresChannelIdAndThreadTimestampWhenResponseUrlIsSupplied()
        {
            var activity = new RichActivity("some text")
            {
                Type = "message",
                Conversation = new(),
                ResponseUrl = new Uri("https://example.com/api/messages"),
            };
            var turnContext = new FakeTurnContext(activity);
            turnContext.SetApiToken(new SecretString("xoxb-whatever-makes-the-test-pass", new FakeDataProtectionProvider()));
            var formatter = new SlackMessageFormatter();

            formatter.FormatOutgoingMessage(activity, turnContext);

            var channelData = Assert.IsType<MessageChannelData>(activity.ChannelData);
            Assert.Empty(channelData.Message.Channel);
            Assert.Null(channelData.Message.ThreadTs);
            Assert.Equal("some text", channelData.Message.Text);
            Assert.Equal("xoxb-whatever-makes-the-test-pass", channelData.ApiToken.Reveal());
            Assert.Null(activity.Text);
        }

        [Theory]
        [InlineData("Hello there", "Hello there")]
        [InlineData("[Abbot](foo bar)", "[Abbot](foo bar)")]
        [InlineData("[Abbot](https://ab.bot/)", "<https://ab.bot/|Abbot>")]
        [InlineData("[Abbot\nTest](https://ab.bot/)", "[Abbot\nTest](https://ab.bot/)")]
        [InlineData("Hello, [Abbot](https://ab.bot/) is your friend.", "Hello, <https://ab.bot/|Abbot> is your friend.")]
        [InlineData("Hello, [Abbot](https://ab.bot/) is your [Friend](https://whatevs).",
            "Hello, <https://ab.bot/|Abbot> is your <https://whatevs|Friend>.")]
        [InlineData("[ALiEn – a GPU-accelerated artificial life simulation program](https://news.ycombinator.com/item?id=27472224)",
            "<https://news.ycombinator.com/item?id=27472224|ALiEn – a GPU-accelerated artificial life simulation program>")]
        public void ConvertsMarkdownLinksToSlackLinks(string text, string expected)
        {
            var activity = new Activity
            {
                Text = text,
                Type = "message",
                TextFormat = "markdown",
                Conversation = new() { Id = new SlackConversationId("C", "123").ToString() },
            };
            var turnContext = new FakeTurnContext(activity);
            turnContext.SetApiToken(new SecretString("xoxb-whatever-makes-the-test-pass", new FakeDataProtectionProvider()));
            var formatter = new SlackMessageFormatter();

            formatter.FormatOutgoingMessage(activity, turnContext);

            var channelData = Assert.IsType<MessageChannelData>(activity.ChannelData);
            Assert.Equal(expected, channelData.Message.Text);
        }

        [Theory]
        [InlineData("B028535TCK0:T013108BYLS:C01A3DGTSP9:12345", "12345", "B028535TCK0:T013108BYLS:C01A3DGTSP9:12345")]
        [InlineData("B028535TCK0:T013108BYLS:C01A3DGTSP9:6789", "6789", "B028535TCK0:T013108BYLS:C01A3DGTSP9:6789")]
        [InlineData("B028535TCK0:T013108BYLS:C01A3DGTSP9", null, "B028535TCK0:T013108BYLS:C01A3DGTSP9")]
        public void PropagatesThreadTsValueFromConversationIdOrThread(string conversationId, string expectedThreadTs, string expectedFinalConvoId)
        {
            var activity = new Activity()
            {
                Type = "message",
                Text = "Hello",
                Conversation = new() { Id = conversationId }
            };
            var turnContext = new FakeTurnContext(activity);
            turnContext.SetApiToken(new SecretString("xoxb-whatever-makes-the-test-pass", new FakeDataProtectionProvider()));
            var formatter = new SlackMessageFormatter();

            formatter.FormatOutgoingMessage(activity, turnContext);

            var cd = Assert.IsType<MessageChannelData>(activity.ChannelData);
            Assert.Equal(expectedThreadTs, cd.Message.ThreadTs);
            Assert.Equal(expectedFinalConvoId, activity.Conversation.Id);
        }
    }

    public class TheNormalizeIncomingMessageMethod
    {
        [Fact]
        public void SetsTextFromChannelData()
        {
            var activity = new Activity
            {
                Text = "@abbot here is @phil",
                Type = "message",
                ChannelData = new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "message",
                            text = "<@U01234567> here is <@U98765432>"
                        }
                    }
                }
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Equal("<@U01234567> here is <@U98765432>", activity.Text);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DoesNothingWhenTextIsNullOrEmpty(string text)
        {
            var activity = new Activity
            {
                Text = "foo",
                Type = "message",
                ChannelData = new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "message",
                            text
                        }
                    }
                }
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Equal("foo", activity.Text);
        }


        [Fact]
        public void IgnoresBlockMessagePayload()
        {
            var channelData = new MessageBlockActionsPayload();
            var activity = new Activity
            {
                Text = "[Abbot](https://ab.bot/)",
                Type = "message",
                TextFormat = "markdown",
                ChannelData = channelData
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Same(channelData, activity.ChannelData);
            Assert.Equal("[Abbot](https://ab.bot/)", activity.Text);
        }

        [Theory]
        [InlineData("<mailto:phil.haack@corp.aseriousbusiness.com|phil@aseriousbusiness.com>", "phil.haack@corp.aseriousbusiness.com")]
        [InlineData("<phil.haack@corp.aseriousbusiness.com|phil@aseriousbusiness.com>", "phil.haack@corp.aseriousbusiness.com")]
        [InlineData("<mailto:phil@aseriousbusiness.com|phil@aseriousbusiness.com>", "phil@aseriousbusiness.com")]
        [InlineData("<phil@aseriousbusiness.com|phil@aseriousbusiness.com>", "phil@aseriousbusiness.com")]
        [InlineData("phil@aseriousbusiness.com", "phil@aseriousbusiness.com")]
        public void NormalizesEmailAddresses(string incoming, string expected)
        {
            var activity = new Activity
            {
                Text = "@abbot here is @phil",
                Type = "message",
                ChannelData = new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "message",
                            text = $"<@U01234567> here is {incoming}"
                        }
                    }
                }
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Equal($"<@U01234567> here is {expected}", activity.Text);
        }

        [Fact]
        public void NormalizesMultipleEmailAddressesInAMessage()
        {
            var activity = new Activity
            {
                Text = "@abbot here is phil@aseriousbusiness.com and paul@aseriousbusiness.com",
                Type = "message",
                ChannelData = new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "message",
                            text = "<@U01234567> here is <mailto:phil@aseriousbusiness.com|phil@aseriousbusiness.com> and <paul@aseriousbusiness.com|paul@aseriousbusiness.com>"
                        }
                    }
                }
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Equal("<@U01234567> here is phil@aseriousbusiness.com and paul@aseriousbusiness.com", activity.Text);
        }

        [Fact]
        public void IgnoresMessagesWithNoChannelData()
        {
            var activity = new Activity
            {
                Text = "@abbot here is @phil",
                Type = "message",
                ChannelData = null
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Equal("@abbot here is @phil", activity.Text);
        }

        [Fact]
        public void IgnoresNonMessageEvents()
        {
            var activity = new Activity
            {
                Name = "vnd.slack.team_join",
                Text = "@abbot here is @phil",
                Type = "message",
                ChannelData = new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "not-message",
                            text = "<@U01234567> here is <@U98765432>"
                        }
                    }
                }
            };
            var formatter = new SlackMessageFormatter();

            formatter.NormalizeIncomingMessage(activity);

            Assert.Equal("@abbot here is @phil", activity.Text);
        }
    }
}

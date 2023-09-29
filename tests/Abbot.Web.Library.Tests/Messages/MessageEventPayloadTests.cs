using System;
using Serious.Abbot.Events;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.Payloads;
using Xunit;

public class MessageEventPayloadTests
{
    public class TheFromSlackMessageEventMethod
    {
        [Theory]
        [InlineData("the Text", "channel", "some-room", null, "threadts", false, true)]
        [InlineData("the Text", "im", "mpdm-group-dm", null, null, true, false)]
        public void WithMessageTypeCreatesEventFromSlackEvent(
            string text,
            string channelType,
            string roomName,
            string? timestamp,
            string? threadTimestamp,
            bool expectedDirectMessage,
            bool expectedIsAttachable)
        {
            var messageEvent = new MessageEvent
            {
                ChannelType = channelType,
                Channel = "C01234567",
                User = "U00111111",
                Timestamp = timestamp,
                ThreadTimestamp = threadTimestamp,
                Blocks = new LayoutBlock[] {
                    new RichTextBlock {
                        BlockId = "blocky-block",
                        Elements = new IElement[] {
                            new TextElement {
                                Text = ".ping ",
                            },
                            new UserMention {
                                UserId = "U0000001",
                            },
                            new TextElement {
                                Text = " ",
                            },
                            new UserMention {
                                UserId = "U0000002",
                            },
                        }
                    }
                }
            };

            var payload = MessageEventInfo.FromSlackMessageEvent(text, messageEvent, "U012345");

            Assert.NotNull(payload);
            Assert.Equal(text, payload.Text);
            Assert.Equal("C01234567", payload.PlatformRoomId);
            Assert.Equal("U00111111", payload.PlatformUserId);
            Assert.Equal(expectedDirectMessage, payload.DirectMessage);
            Assert.Equal(timestamp, payload.MessageId);
            Assert.Equal(threadTimestamp, payload.ThreadId);
            Assert.Collection(payload.MentionedUserIds,
                user0Id => Assert.Equal("U0000001", user0Id),
                user1Id => Assert.Equal("U0000002", user1Id));
        }

        [Theory]
        [InlineData("the Text", "im", "some-room", "ts", null, true, false)]
        [InlineData("the Text", "channel", "mpdm-group-dm", "ts", "threadTs", false, false)]
        public void WithAppMentionTypeCreatesEventFromSlackEvent(
            string text,
            string channelType,
            string roomName,
            string? timestamp,
            string? threadTimestamp,
            bool expectedDirectMessage,
            bool expectedIsAttachable)
        {
            const string botUserId = "U012345";
            var messageEvent = new AppMentionEvent
            {
                Channel = "C01234567",
                User = "U00111111",
                ChannelType = channelType,
                Timestamp = timestamp,
                ThreadTimestamp = threadTimestamp,
                Blocks = new LayoutBlock[] {
                    new RichTextBlock {
                        BlockId = "blocky-block",
                        Elements = new IElement[] {
                            new TextElement {
                                Text = ".ping ",
                            },
                            new UserMention {
                                UserId = "U0000001",
                            },
                            new TextElement {
                                Text = " ",
                            },
                            new UserMention {
                                UserId = botUserId,
                            },
                            new TextElement {
                                Text = " ",
                            },
                            new UserMention {
                                UserId = "U0000002",
                            },
                        }
                    }
                }
            };

            var payload = MessageEventInfo.FromSlackMessageEvent(text, messageEvent, botUserId);

            Assert.NotNull(payload);
            Assert.Equal(text, payload.Text);
            Assert.Equal("C01234567", payload.PlatformRoomId);
            Assert.Equal("U00111111", payload.PlatformUserId);
            Assert.Equal(expectedDirectMessage, payload.DirectMessage);
            Assert.Equal(timestamp, payload.MessageId);
            Assert.Equal(threadTimestamp, payload.ThreadId);
            Assert.Collection(payload.MentionedUserIds,
                user0Id => Assert.Equal("U0000001", user0Id),
                user1Id => Assert.Equal("U0000002", user1Id));
        }

        [Theory]
        [InlineData("channel", false, false)]
        [InlineData("im", true, false)] // Never ignore direct messages
        public void WithMessageEventTypeThatDoesNotMentionBotSetIgnoreFalse(
            string channelType,
            bool expectedDirectMessage,
            bool expectedIgnore)
        {
            const string botUserId = "U1234567";
            var messageEvent = new MessageEvent
            {
                ChannelType = channelType,
                Channel = "C01234567",
                User = "U00111111",
                Timestamp = "ts",
                ThreadTimestamp = "thread.ts",
                Blocks = Array.Empty<LayoutBlock>()
            };

            var payload = MessageEventInfo.FromSlackMessageEvent(string.Empty, messageEvent, botUserId);

            Assert.NotNull(payload);
            Assert.Equal(expectedIgnore, payload.Ignore);
            Assert.Equal(expectedDirectMessage, payload.DirectMessage);
        }

        [Theory]
        [InlineData("im", true, false)]
        [InlineData("channel", false, true)] // Ignore message events that mention abbot as app_mention will also be raised in this situation and we want to handle that one.
        public void WithMessageEventTypeThatAlsoMentionBotSetIgnoreTrue(
            string channelType,
            bool expectedDirectMessage,
            bool expectedIgnore)
        {
            const string botUserId = "U1234567";
            var messageEvent = new MessageEvent
            {
                Channel = "C01234567",
                User = "U00111111",
                ChannelType = channelType,
                Timestamp = "ts",
                ThreadTimestamp = "thread.ts",
                Blocks = new LayoutBlock[] {
                    new RichTextBlock {
                        Elements = new Element[] {
                            new TextElement {
                                Text = "the Text"
                            },
                            new UserMention {
                                UserId = botUserId
                            }
                        }
                    }
                }
            };

            var payload = MessageEventInfo.FromSlackMessageEvent(
                string.Empty,
                messageEvent,
                botUserId);

            Assert.NotNull(payload);
            Assert.Equal(expectedIgnore, payload.Ignore);
            Assert.Equal(expectedDirectMessage, payload.DirectMessage);
        }

        [Fact]
        public void WithBotWorkflowMessageIsIgnored()
        {
            const string botUserId = "U1234567";
            var messageEvent = new BotMessageEvent
            {
                Channel = "C01234567",
                User = "U00111111",
                Timestamp = "ts",
                BotProfile = new BotProfile(
                    Id: "B012344",
                    Deleted: false,
                    Name: "Workflow Name",
                    Updated: "12341234",
                    AppId: "A01234",
                    IsWorkflowBot: true,
                    TeamId: "T0001",
                    Icons: new BotIcons(null, null, null)),
                ThreadTimestamp = "thread.ts",
                Blocks = new LayoutBlock[] {
                    new RichTextBlock {
                        Elements = new Element[] {
                            new TextElement {
                                Text = "the Text"
                            },
                            new UserMention {
                                UserId = botUserId
                            }
                        }
                    }
                }
            };

            var payload = MessageEventInfo.FromSlackMessageEvent(
                string.Empty,
                messageEvent,
                botUserId);

            Assert.NotNull(payload);
            Assert.True(payload.Ignore);
            Assert.False(payload.DirectMessage);
            Assert.True(payload.WorkflowMessage);
        }

        [Theory]
        [InlineData("im", true)]
        [InlineData("channel", false)]
        public void WithAppMentionTypeNeverSetsIgnoreTrue(string channelType, bool expectedDirectMessage)
        {
            var messageEvent = new AppMentionEvent
            {
                Channel = "C01234567",
                User = "U00111111",
                ChannelType = channelType,
                Timestamp = "ts",
                ThreadTimestamp = "thread.ts",
            };

            var payload = MessageEventInfo.FromSlackMessageEvent(string.Empty, messageEvent, "U12345");

            Assert.NotNull(payload);
            Assert.False(payload.Ignore);
            Assert.Equal(expectedDirectMessage, payload.DirectMessage);
        }
    }
}

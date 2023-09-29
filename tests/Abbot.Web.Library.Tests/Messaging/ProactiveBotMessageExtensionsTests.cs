using System;
using System.Linq;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Slack.BlockKit;
using Serious.Text;

public class ProactiveBotMessageExtensionsTests
{
    public class TheTranslateToRequestMethod
    {
        [Fact]
        public void TranslatesMessageToBotMessageRequest()
        {
            var message = new ProactiveBotMessage
            {
                Message = "Hello world",
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress
                    {
                        Id = "C00000017",
                        ThreadId = "12345679.12345678",
                        MessageId = "9876532.12345"
                    }
                },
            };

            var messageRequest = message.TranslateToRequest();

            Assert.Equal("Hello world", messageRequest.Text);
            Assert.Equal(message.Options.To, messageRequest.To);
            Assert.Null(messageRequest.Blocks);
            Assert.Null(messageRequest.Attachments);
            Assert.Null(messageRequest.ImageUpload);
        }

        [Theory]
        [InlineData("B01234:T0123423:C01234143", "C01234143", null)]
        [InlineData("::C01234143", "C01234143", null)]
        [InlineData("B01234:T0123423:C01234143:123241234.12343", "C01234143", "123241234.12343")]
        [InlineData("::C01234143:123241234.12343", "C01234143", "123241234.12343")]
        public void TranslatesConversationReferenceCorrectly(
            string conversationId,
            string expectedChannel,
            string? expectedThreadId)
        {
            var message = new ProactiveBotMessage
            {
                Message = "Hello world",
                ConversationReference = new ConversationReference
                {
                    Conversation = new()
                    {
                        Id = conversationId
                    }
                }
            };

            var messageRequest = message.TranslateToRequest();

            Assert.Equal("Hello world", messageRequest.Text);
            Assert.Equal(expectedChannel, messageRequest.To.Id);
            Assert.Equal(expectedThreadId, messageRequest.To.ThreadId);
            Assert.Null(messageRequest.Blocks);
            Assert.Null(messageRequest.Attachments);
            Assert.Null(messageRequest.ImageUpload);
        }

        [Fact]
        public void TranslatesBlocksInMessage()
        {
            var blocks = new object[]
            {
                new {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = "*text*"
                    }
                },
                new {
                    type = "divider"
                }
            };

            var message = new ProactiveBotMessage
            {
                Message = "Hello world",
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress
                    {
                        Id = "C00000017",
                        ThreadId = "12345679.12345678",
                        MessageId = "9876532.12345"
                    }
                },
                Blocks = JsonConvert.SerializeObject(blocks),
            };

            var messageRequest = message.TranslateToRequest();

            Assert.Equal("Hello world", messageRequest.Text);
            Assert.NotNull(messageRequest.Blocks);
            Assert.Collection(messageRequest.Blocks,
                b0 => {
                    var section = Assert.IsType<Section>(b0);
                    Assert.Equal("*text*", section.Text?.Text);
                },
                b1 => Assert.IsType<Divider>(b1));
        }

        [Fact]
        public void TranslatesBlocksWithBlockKitContainerObject()
        {
            var blocksContainer = new {
                blocks = new object[] {
                    new {
                        type = "section",
                        text = new
                        {
                            type = "mrkdwn",
                            text = "*text*"
                        }
                    }
                }
            };
            var message = new ProactiveBotMessage
            {
                Message = "Hello world",
                Blocks = JsonConvert.SerializeObject(blocksContainer),
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress(ChatAddressType.Room, "C0123412324")
                }
            };

            var messageRequest = message.TranslateToRequest();

            Assert.Equal("Hello world", messageRequest.Text);
            Assert.NotNull(messageRequest.Blocks);
            var block = Assert.IsType<Section>(Assert.Single(messageRequest.Blocks));
            Assert.NotNull(block.Text);
            Assert.Equal("*text*", block.Text.Text);
        }

        [Fact]
        public async Task WritesBlockIdSoWeCanRouteItBackToSkillButIgnoresOurRouting()
        {
            var blocks = new object[] {
                new {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = "*text*"
                    }
                },
                new {
                    type = "divider"
                },
                new
                {
                    type = "actions",
                    block_id = "customer-supplied-id",
                    elements = new object[]
                    {
                        new {
                            type = "button",
                            text = new {
                                type = "plain_text",
                                text = "Click Me"
                            },
                        }
                    }
                },
                new
                {
                    type = "actions",
                    block_id = "i:TicketHandler",
                    elements = new object[]
                    {
                        new {
                            type = "button",
                            text = new {
                                type = "plain_text",
                                text = "Click Me"
                            },
                        }
                    }
                }
            };
            var message = new ProactiveBotMessage
            {
                SkillId = 42,
                Message = "Hello world",
                Blocks = JsonConvert.SerializeObject(blocks),
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress(ChatAddressType.Room, "C0123412324")
                }
            };

            var messageRequest = message.TranslateToRequest();

            Assert.Equal("Hello world", messageRequest.Text);
            Assert.NotNull(messageRequest.Blocks);
            var blockIds = messageRequest.Blocks.Select(b => b.BlockId).GroupBy(b => b);
            Assert.False(blockIds.Any(g => g.Count() > 1)); // Make sure they're all unique.
            Assert.Collection(messageRequest.Blocks,
                b0 => {
                    Assert.NotNull(b0.BlockId);
                    var wrapped = WrappedValue.Parse(b0.BlockId);
                    Assert.Equal("s:42", wrapped.ExtraInformation);
                    // Even though the customer didn't supply an original value, block IDs must be unique.
                    // So we need to generate unique values for each block.
                    Assert.NotNull(wrapped.OriginalValue);
                    Assert.NotEmpty(wrapped.OriginalValue);
                    var section = Assert.IsType<Section>(b0);
                    // Since the block ID is not set, it's randomly generated, hence the assert of the length.s
                    Assert.Equal(9, section.BlockId?.Length);
                    Assert.StartsWith("s:42|", section.BlockId);
                    Assert.NotNull(section.Text);
                    Assert.Equal("*text*", section.Text.Text);
                },
                b1 => {
                    Assert.IsType<Divider>(b1);
                    Assert.StartsWith("s:42", b1.BlockId);
                },
                b3 => {
                    var actionsBlock = Assert.IsType<Actions>(b3);
                    Assert.Equal("s:42|customer-supplied-id", actionsBlock.BlockId);
                },
                b4 => {
                    var actionsBlock = Assert.IsType<Actions>(b4);
                    Assert.Equal("i:TicketHandler", actionsBlock.BlockId);
                });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("{\"blocks\":[]}")]
        public async Task SetsBlockNullWhenBlocksJsonNullOrEmpty(string? blocksJson)
        {
            var message = new ProactiveBotMessage
            {
                Message = "Hello world",
                Blocks = blocksJson,
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress(ChatAddressType.Room, "C0123412324")
                }
            };

            var messageRequest = message.TranslateToRequest();

            Assert.Equal("Hello world", messageRequest.Text);
            Assert.Null(messageRequest.Blocks);
        }

        [Fact]
        public void TranslatesBase64EncodedImageAttachment()
        {
            var message = new ProactiveBotMessage
            {
                Message = "Hello world",
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress
                    {
                        Id = "C00000017",
                        ThreadId = "12345679.12345678",
                        MessageId = "9876532.12345"
                    }
                },
                Attachments = new[]
                {
                    new MessageAttachment
                    {
                        ImageUrl = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
                        Title = "Abbot",
                    }
                }
            };

            var messageRequest = message.TranslateToRequest();

            Assert.NotNull(messageRequest.ImageUpload);
            Assert.Equal("Abbot", messageRequest.ImageUpload.Title);
            Assert.Equal(68, messageRequest.ImageUpload.ImageBytes.Length);
            Assert.Equal(message.Attachments[0].ImageUrl, Convert.ToBase64String(messageRequest.ImageUpload.ImageBytes));
        }
    }
}

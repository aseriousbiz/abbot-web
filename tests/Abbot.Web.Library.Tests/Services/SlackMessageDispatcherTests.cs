using System;
using System.Linq;
using Abbot.Common.TestHelpers;
using Newtonsoft.Json;
using NSubstitute;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Text;

public class SlackMessageDispatcherTests
{
    public class TheDispatchMessageAsyncMethod
    {
        [Fact]
        public async Task DispatchesMessageWithFormattedLinks()
        {
            var env = TestEnvironment.Create();
            var message = new BotMessageRequest(
                "Hello [Abbot](https://ab.bot)",
                new ChatAddress(ChatAddressType.Room, "C01234567"));
            var dispatcher = env.Activate<SlackMessageDispatcher>();

            await dispatcher.DispatchAsync(message, env.TestData.Organization);

            var messageRequest = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Null(messageRequest.UserName);
            Assert.Equal("C01234567", messageRequest.Channel);
            Assert.Null(messageRequest.ThreadTs);
            Assert.NotNull(messageRequest.Timestamp);
            Assert.Equal("Hello <https://ab.bot|Abbot>", messageRequest.Text);
            Assert.Null(messageRequest.Blocks);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("1234567890.223456")]
        public async Task UpdatesExistingMessage(string? threadId)
        {
            var env = TestEnvironment.Create();
            var message = new BotMessageRequest(
                "Hello [Abbot](https://ab.bot)",
                new ChatAddress(ChatAddressType.Room, "C01234567", ThreadId: threadId, MessageId: "1234567890.123456"));
            var dispatcher = env.Activate<SlackMessageDispatcher>();

            await dispatcher.DispatchAsync(message, env.TestData.Organization);

            Assert.Empty(env.SlackApi.PostedMessages);
            var messageRequest = Assert.Single(env.SlackApi.UpdatedMessages);
            Assert.Equal("1234567890.123456", messageRequest.Timestamp);
            Assert.Equal(threadId, messageRequest.ThreadTs);
            Assert.Null(messageRequest.UserName);
            Assert.Equal("C01234567", messageRequest.Channel);
            Assert.Equal("Hello <https://ab.bot|Abbot>", messageRequest.Text);
            Assert.Null(messageRequest.Blocks);
        }

        [Fact]
        public async Task DispatchesEphemeralMessage()
        {
            var env = TestEnvironment.Create();
            var message = new BotMessageRequest(
                "Hello friends!",
                new ChatAddress(ChatAddressType.Room, "C01234567", EphemeralUser: "U0123242314"));
            var dispatcher = env.Activate<SlackMessageDispatcher>();

            await dispatcher.DispatchAsync(message, env.TestData.Organization);

            var messageRequest = Assert.IsType<EphemeralMessageRequest>(Assert.Single(env.SlackApi.PostedMessages));
            Assert.Equal("U0123242314", messageRequest.User);
            Assert.Null(messageRequest.UserName);
            Assert.Equal("C01234567", messageRequest.Channel);
            Assert.Null(messageRequest.ThreadTs);
            Assert.Null(messageRequest.Timestamp);
            Assert.Equal("Hello friends!", messageRequest.Text);
            Assert.Null(messageRequest.Blocks);
        }

        [Fact]
        public async Task PassesBlocksToMessageRequest()
        {
            var env = TestEnvironment.Create();
            var message = new BotMessageRequest(
                "Hello world",
                new ChatAddress(ChatAddressType.Room, "C01234567"),
                Blocks: new ILayoutBlock[]
                {
                    new Section(new MrkdwnText("*text*")),
                    new Divider()
                });
            var dispatcher = env.Activate<SlackMessageDispatcher>();

            await dispatcher.DispatchAsync(message, env.TestData.Organization);

            var messageRequest = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal("Hello world", messageRequest.Text);
            Assert.NotNull(messageRequest.Blocks);
            Assert.Collection(messageRequest.Blocks,
                b0 => Assert.IsType<Section>(b0),
                b1 => Assert.IsType<Divider>(b1));
        }

        [Theory]
        [InlineData(null, null, "unnamed-file")]
        [InlineData("Abbot", null, "Abbot")]
        [InlineData(null, "12343214.3432", "unnamed-file")]
        [InlineData("Abbot", "8675309.1322", "Abbot")]
        public async Task UploadsImageWhenBase64EncodedAttachmentExists(
            string? imageTitle,
            string? threadTimestamp,
            string expectedFileName)
        {
            var env = TestEnvironment.Create();
            var imageUpload = new ImageUpload(new byte[] { 42, 23 }, imageTitle);
            var chatAddress = new ChatAddress(ChatAddressType.Room, "C01234566", threadTimestamp);
            var messageRequest = new BotMessageRequest("Hello world", chatAddress, ImageUpload: imageUpload);
            env.SlackApi.Files.UploadFileAsync(Args.String,
                    Arg.Any<StreamPart>(),
                    Args.String,
                    Args.String,
                    Args.String,
                    Args.String,
                    Args.String)
                .Returns(new FileResponse
                {
                    Ok = true,
                });
            var dispatcher = env.Activate<SlackMessageDispatcher>();

            await dispatcher.DispatchAsync(messageRequest, env.TestData.Organization);

            await env.SlackApi.Files.Received()
                .UploadFileAsync(accessToken: Args.String,
                    file: Arg.Any<StreamPart>(),
                    filename: expectedFileName,
                    filetype: Args.String,
                    channels: "C01234566",
                    initialComment: "Hello world",
                    threadTimestamp: Arg.Is<string?>(v => v == threadTimestamp));
        }


    }
}

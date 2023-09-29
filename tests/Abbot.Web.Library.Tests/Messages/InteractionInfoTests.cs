using System;
using System.Collections.Generic;
using Serious.Abbot.Events;
using Serious.Payloads;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Xunit;

public class InteractionInfoTests
{
    public class TheConstructor
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CreatesMessageInteractionInfoFromMessageBlockActionsPayload(bool ephemeral)
        {
            var payload = new MessageBlockActionsPayload
            {
                Container = new MessageContainer("some-timestamp", ephemeral, "channel-id"),
                ResponseUrl = new Uri("https://www.example.com/response"),
                Message = new SlackMessage
                {
                    Text = "source-text"
                },
                Actions = new List<IPayloadElement>
                {
                    new ButtonElement()
                }
            };
            var callbackInfo = new InteractionCallbackInfo("SomeHandler");

            var interactionInfo = new MessageInteractionInfo(payload, string.Empty, callbackInfo);

            Assert.Equal(string.Empty, interactionInfo.Arguments);
            Assert.Equal("some-timestamp", interactionInfo.ActivityId);
            Assert.Equal(new Uri("https://www.example.com/response"), interactionInfo.ResponseUrl);
            Assert.Equal("source-text", interactionInfo.SourceMessage?.Text);
            Assert.IsType<ButtonElement>(interactionInfo.ActionElement);
            Assert.Equal(ephemeral, interactionInfo.Ephemeral);
        }

        [Fact]
        public void CreatesMessageInteractionInfoFromMessageActionPayload()
        {
            var payload = new MessageActionPayload
            {
                MessageTimestamp = "some-timestamp",
                ResponseUrl = new Uri("https://www.example.com/response"),
                Message = new SlackMessage { Text = "source-text" }
            };
            var callbackInfo = new InteractionCallbackInfo("SomeHandler");

            var interactionInfo = new MessageInteractionInfo(payload, string.Empty, callbackInfo);

            Assert.Equal(string.Empty, interactionInfo.Arguments);
            Assert.Equal("some-timestamp", interactionInfo.ActivityId);
            Assert.Equal("source-text", interactionInfo.SourceMessage?.Text);
            Assert.Null(interactionInfo.ActionElement);
            Assert.False(interactionInfo.Ephemeral);
        }

        [Fact]
        public void CreatesMessageInteractionInfoFromInteractiveMessagePayload()
        {
            var payload = new InteractiveMessagePayload
            {
                MessageTimestamp = "some-timestamp",
                ResponseUrl = new Uri("https://www.example.com/response"),
                OriginalMessage = new() { Text = "source-text" }
            };
            var callbackInfo = new InteractionCallbackInfo("SomeHandler");

            var interactionInfo = new MessageInteractionInfo(payload, "foo", callbackInfo);

            Assert.Equal("foo", interactionInfo.Arguments);
            Assert.Equal("some-timestamp", interactionInfo.ActivityId);
            Assert.Equal(new Uri("https://www.example.com/response"), interactionInfo.ResponseUrl);
            Assert.Equal("source-text", interactionInfo.SourceMessage?.Text);
            Assert.Null(interactionInfo.ActionElement);
            Assert.False(interactionInfo.Ephemeral);
        }
    }
}

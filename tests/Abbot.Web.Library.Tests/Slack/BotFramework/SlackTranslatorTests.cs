using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Xunit;

public class SlackTranslatorTests
{
    public class TheGetConversationIdFromInteractionPayloadMethod
    {
        [Theory]
        [InlineData(null, "::C12345678")]
        [InlineData("12345.56", "::C12345678:12345.56")]
        public void TranslatesMessagePayloadToConversationId(string threadTimestamp, string expected)
        {
            var payload = new InteractiveMessagePayload
            {
                Team = new TeamIdentifier
                {
                    Id = "T12345678"
                },
                Channel = new ChannelInfo
                {
                    Id = "C12345678"
                },
                OriginalMessage = new()
                {
                    ThreadTimestamp = threadTimestamp
                }
            };

            var conversationId = SlackTranslator.GetConversationIdFromInteractionPayload(payload);

            Assert.Equal(expected, conversationId);
        }

        [Fact]
        public void ReturnsNullWhenChannelIdNull()
        {
            var payload = new ViewClosedPayload
            {
                Team = new TeamIdentifier
                {
                    Id = "T12345678"
                }
            };

            var conversationId = SlackTranslator.GetConversationIdFromInteractionPayload(payload);

            Assert.Null(conversationId);
        }
    }
}

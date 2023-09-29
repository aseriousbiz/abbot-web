using Serious.Abbot.Messaging;
using Xunit;

public class SlackConversationIdTests
{
    public class TheTryParseMethod
    {
        [Theory]
        [InlineData("B0001:T0001:C0001", "C0001", null)]
        [InlineData("B0001:T0001:C0001:12345.769", "C0001", "12345.769")]
        [InlineData("::C0001", "C0001", null)]
        public void CanParseConversationIds(
            string id,
            string expectedChannelId,
            string? expectedThreadTimestamp)
        {
            Assert.True(SlackConversationId.TryParse(id, out var parsed));
            Assert.Equal(expectedChannelId, parsed.ChannelId);
            Assert.Equal(expectedThreadTimestamp, parsed.ThreadTimestamp);
        }

        [Theory]
        [InlineData("C0001", "C0001")]
        [InlineData("U0001", "U0001")]
        public void CanParseNonHierarchicalIds(
            string id,
            string expectedChannelId)
        {
            Assert.True(SlackConversationId.TryParse(id, out var parsed));
            Assert.Equal(expectedChannelId, parsed.ChannelId);
            Assert.Null(parsed.ThreadTimestamp);
        }
    }

    public class TheToStringMethod
    {
        [Theory]
        [InlineData("C0001", null, "::C0001")]
        [InlineData("C0001", "12345.798", "::C0001:12345.798")]
        public void RendersHierarchicalId(string channelId, string? threadTimestamp, string expected)
        {
            var id = new SlackConversationId(channelId, threadTimestamp);
            Assert.Equal(expected, id.ToString());
        }
    }
}

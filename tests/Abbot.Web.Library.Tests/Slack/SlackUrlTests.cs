using Serious;
using Xunit;

public class SlackUrlTests
{
    public class TheTryParseMethod
    {
        [Theory]
        [InlineData("https://aseriousbiz.slack.com/archives/C012ZJGPYTF/p1639006342178800?thread_ts=1639006311.178500&cid=C012ZJGPYTF", "C012ZJGPYTF", "1639006342.178800", "1639006311.178500")]
        [InlineData("https://aseriousbiz.slack.com/archives/C012ZJGPYTF/p1639006311178500", "C012ZJGPYTF", "1639006311.178500", null)]
        public void ParsesMessageUrls(string url, string channelId, string messageTs, string? threadTs)
        {
            Assert.True(SlackUrl.TryParse(url, out var parsed));
            var messageUrl = Assert.IsType<SlackMessageUrl>(parsed);
            Assert.Equal(channelId, messageUrl.ConversationId);
            Assert.Equal(messageTs, messageUrl.Timestamp);
            Assert.Equal(threadTs, messageUrl.ThreadTimestamp);
            Assert.Equal(url, messageUrl.Url.ToString());
        }

        [Theory]
        [InlineData("https://aseriousbiz.slack.com/archives/C012ZJGPYTF", "C012ZJGPYTF")]
        [InlineData("https://aseriousbiz.slack.com/archives/G0987654321", "G0987654321")]
        [InlineData("https://aseriousbiz.slack.com/archives/D02LV16PBE3", "D02LV16PBE3")]
        public void ParsesConversationUrls(string url, string conversationId)
        {
            Assert.True(SlackUrl.TryParse(url, out var parsed));
            var messageUrl = Assert.IsType<SlackConversationUrl>(parsed);
            Assert.Equal(conversationId, messageUrl.ConversationId);
        }

        [Theory]
        [InlineData("https://aseriousbiz.slack.com/team/U02EMN2AYGH", "U02EMN2AYGH")]
        public void ParsesUserUrls(string url, string userId)
        {
            Assert.True(SlackUrl.TryParse(url, out var parsed));
            var messageUrl = Assert.IsType<SlackUserUrl>(parsed);
            Assert.Equal(userId, messageUrl.UserId);
        }
    }
}

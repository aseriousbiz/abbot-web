using System.Globalization;
using Serious.Abbot;

public class SlackFormatterTests
{
    [Fact]
    public void UserMentionSyntaxTest()
    {
        Assert.Equal("<@U001>", SlackFormatter.UserMentionSyntax("U001"));
    }

    [Theory]
    [InlineData(null, "C001", true, "The organization domain must be specified when platform type is 'Slack' (Parameter 'organizationDomain')")]
    [InlineData("org.example.com", "C001", false, "https://org.example.com/archives/C001")]
    public void RoomUrlTest(string? organizationDomain, string roomId, bool throws, string expected)
    {
        if (throws)
        {
            var ex = Assert.ThrowsAny<Exception>(() => SlackFormatter.RoomUrl(organizationDomain, roomId));
            Assert.Equal(expected, ex.Message);
        }
        else
        {
            Assert.Equal(new Uri(expected), SlackFormatter.RoomUrl(organizationDomain, roomId));
        }
    }

    [Theory]
    [InlineData(null, "C001", "1111.2222", null, true, "The organization domain must be specified when platform type is 'Slack' (Parameter 'organizationDomain')")]
    [InlineData("org.example.com", "C001", "1111.2222", null, false, "https://org.example.com/archives/C001/p11112222")]
    [InlineData("org.example.com", "C001", "1111.2222", "3333.4444", false, "https://org.example.com/archives/C001/p11112222?thread_ts=3333.4444")]
    public void MessageUrlTest(string? organizationDomain, string roomId, string messageId, string? threadId, bool throws, string expected)
    {
        if (throws)
        {
            var ex = Assert.ThrowsAny<Exception>(() => SlackFormatter.MessageUrl(organizationDomain, roomId, messageId, threadId));
            Assert.Equal(expected, ex.Message);
        }
        else
        {
            Assert.Equal(new Uri(expected), SlackFormatter.MessageUrl(organizationDomain, roomId, messageId, threadId));
        }
    }

    [Theory]
    [InlineData(null, "U001", true, "The organization domain must be specified when platform type is 'Slack' (Parameter 'organizationDomain')")]
    [InlineData("org.example.com", "U001", false, "https://org.example.com/team/U001")]
    public void UserUrlTest(string? organizationDomain, string userId, bool throws, string? expected)
    {
        if (throws)
        {
            var ex = Assert.ThrowsAny<Exception>(() => SlackFormatter.UserUrl(organizationDomain, userId));
            Assert.Equal(expected, ex.Message);
        }
        else
        {
            Assert.Equal(expected is null ? null : new Uri(expected), SlackFormatter.UserUrl(organizationDomain, userId));
        }
    }

    public class TheGetDateFromSlackTimestampMethod
    {
        [Fact]
        public void CanConvertTimestampToUtcDateTime()
        {
            const string ts = "1656527452.995229";

            var result = SlackFormatter.GetDateFromSlackTimestamp(ts);

            Assert.Equal("2022-06-29 18:30:52", result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }

    public class TheToSlackChannelNameMethod
    {
        [Theory]
        [InlineData("Hello World", "hello-world")]
        [InlineData("Hello123World!", "hello123world")]
        [InlineData("###channel- name###", "channel-name")]
        [InlineData("C#Programming_Language is Cool", "c-programming_language-is-cool")]
        [InlineData("C##Programming_Language is Cool", "c-programming_language-is-cool")]
        [InlineData("Some Channel_Name!", "some-channel_name")]
        [InlineData("Ten-Chars-Ten-Chars-Ten-Chars-Ten-Chars-Ten-Chars-Ten-Chars-Ten-Chars-Ten-Chars-Ten-Chars", "ten-chars-ten-chars-ten-chars-ten-chars-ten-chars-ten-chars-ten-chars-ten-chars-")]
        public void CanConvertStringToValidChannelName(string value, string expected)
        {
            var result = value.ToSlackChannelName();

            Assert.Equal(expected, result);
        }
    }
}

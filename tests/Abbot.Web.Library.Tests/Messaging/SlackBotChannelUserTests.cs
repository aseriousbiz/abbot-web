using Serious.Abbot.Messaging;
using Xunit;

public class SlackBotChannelUserTests
{
    public class TheConstructor
    {
        [Theory]
        [InlineData(null, "abbot")]
        [InlineData("a-bot", "a-bot")]
        public void CreatesBotUserFromChannelAccount(string? botName, string expectedUserName)
        {
            var bot = new SlackBotChannelUser("T001", "B001", "U001", botName);

            Assert.Equal("T001", bot.PlatformId);
            Assert.Equal("B001", bot.Id);
            Assert.Equal("U001", bot.UserId);
            Assert.Equal(expectedUserName, bot.DisplayName);
            Assert.Equal("<@U001>", bot.ToString());
        }
    }
}

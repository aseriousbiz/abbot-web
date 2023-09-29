using Microsoft.Bot.Schema;
using Serious.Abbot.Messaging;
using Xunit;

public class SlackPartialBotChannelUserTests
{
    public class TheConstructor
    {
        [Theory]
        [InlineData(null, "abbot")]
        [InlineData("a-bot", "a-bot")]
        public void CreatesBotUserFromChannelAccount(string? botName, string expectedUserName)
        {
            var bot = new SlackPartialBotChannelUser("T001", "B001", botName);

            Assert.Equal("T001", bot.PlatformId);
            Assert.Equal("B001", bot.Id);
            Assert.Equal("B001", bot.UserId);
            Assert.Equal(expectedUserName, bot.DisplayName);
            Assert.Equal($"@{expectedUserName}", bot.ToString());
        }
    }
}

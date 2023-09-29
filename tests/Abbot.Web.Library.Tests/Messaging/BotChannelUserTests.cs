using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Scripting;
using Xunit;

public class BotChannelUserTests
{
    public class TheGetBotUserMethod
    {
        [Fact]
        public void CanCreateSlackBotChannelUserFromOrganizationAndBotName()
        {
            var env = TestEnvironment.CreateWithoutData();
            var apiToken = env.Secret("xoxb-token");

            var organization = new Organization
            {
                PlatformId = "T123",
                BotName = "abbot-unit-test",
                PlatformBotId = "B0987",
                PlatformBotUserId = "U01234",
                PlatformType = PlatformType.Slack,
                BotResponseAvatar = "avi",
                ApiToken = apiToken,
                Scopes = "mouthwash",
            };

            var botUser = BotChannelUser.GetBotUser(organization);

            Assert.Equal("T123", botUser.PlatformId);
            Assert.Equal("B0987", botUser.Id);
            Assert.Equal("U01234", botUser.UserId);
            Assert.Equal("abbot-unit-test", botUser.DisplayName);
            Assert.Equal(apiToken, botUser.ApiToken);
            Assert.Equal("mouthwash", botUser.Scopes);
            Assert.Equal("avi", botUser.BotResponseAvatar);
            Assert.IsType<SlackBotChannelUser>(botUser);
        }

        [Fact]
        public void CanCreateSlackPartialBotChannelUserFromOrganizationAndBotNameWhenBotUserIdNull()
        {
            var organization = new Organization
            {
                PlatformId = "T123",
                BotName = "abbot-unit-test",
                PlatformBotId = "B0987",
                PlatformType = PlatformType.Slack
            };

            var botUser = BotChannelUser.GetBotUser(organization);

            Assert.Equal("T123", botUser.PlatformId);
            Assert.Equal("B0987", botUser.Id);
            Assert.Equal("B0987", botUser.UserId);
            Assert.Equal("abbot-unit-test", botUser.DisplayName);
            Assert.IsType<SlackPartialBotChannelUser>(botUser);
        }
    }

    public class TheTryGetUnprotectedApiTokenMethod
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("token", "token")]
        public void RevealsToken(string? apiToken, string? expected)
        {
            var env = TestEnvironment.CreateWithoutData();

            var secret = env.Secret(apiToken);
            var bot = new BotChannelUser("T001", "B001", "abbot", secret);

            Assert.Equal(expected is not null, bot.TryGetUnprotectedApiToken(out var unprotected));
            Assert.Equal(expected, unprotected);
        }
    }
}

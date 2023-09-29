using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Slack;
using Xunit;

public class UserEventPayloadTests
{
    public class TheFromSlackUserInfoMethod
    {
        [Theory]
        [InlineData("D", "R", "D")]
        [InlineData(null, "R", "R")]
        [InlineData("", "R", "R")]
        [InlineData(null, null, "N")]
        [InlineData(null, "", "N")]
        [InlineData("", null, "N")]
        [InlineData("", "", "N")]
        public void SetsDisplayNameToDisplayNameOrRealNameOrName(string? displayName, string? realName, string expected)
        {
            var user = new UserInfo
            {
                Id = "id1",
                Profile = new UserProfile
                {
                    DisplayName = displayName,
                    RealName = realName,
                    DisplayNameNormalized = "ignored",
                    RealNameNormalized = "ignored",
                },
                Name = "N"
            };

            var result = UserEventPayload.FromSlackUserInfo(user);

            Assert.Equal(expected, result.DisplayName);
            Assert.Equal(realName ?? "", result.RealName);
        }
    }
}

using Serious.Abbot.Events;
using Serious.Slack;
using Xunit;

public class TeamInfoExtensionsTests
{
    public class TheGetAvatarMethod
    {
        [Fact]
        public void ReturnsImage68()
        {
            var teamInfo = new TeamInfo
            {
                Id = "id1",
                Domain = "slug",
                Icon = new Icon
                {
                    Image68 = "https://example.com/icon.png",
                },
            };

            var avatar = teamInfo.GetAvatar();

            Assert.Equal("https://example.com/icon.png", avatar);
        }
    }

    public class TheGetEnterpriseIdMethod
    {
        [Theory]
        [InlineData("T0123456", null, "")]
        [InlineData("T0123456", "E000011121", "E000011121")]
        [InlineData("E000011121", null, "E000011121")]
        public void ReturnsEnterpriseGridId(string teamId, string? enterpriseId, string expected)
        {
            var teamInfo = new TeamInfo
            {
                Id = teamId,
                EnterpriseId = enterpriseId,
                Domain = "slug",
                Icon = new Icon
                {
                    Image68 = "https://example.com/icon.png",
                },
            };

            var enterpriseGridId = teamInfo.GetEnterpriseId();

            Assert.Equal(expected, enterpriseGridId);
        }
    }

    public class TheGetHostNameMethod
    {
        [Fact]
        public void ReturnsHostName()
        {
            var teamInfo = new TeamInfo
            {
                Id = "id1",
                Domain = "slug",
                Icon = new Icon
                {
                    Image68 = "https://example.com/icon.png",
                },
            };

            var domain = teamInfo.GetHostName();

            Assert.Equal("slug.slack.com", domain);
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Scripting;
using Serious.Abbot.Skills;
using Serious.TestHelpers;
using Xunit;

public class MySkillTests
{
    public class TheMyCommandWithEmailProperty
    {
        [Theory]
        [InlineData("pjh@example.com", "pjh@example.com")]
        [InlineData("<mailto:pjh@example.com|pjh@example.com>", "pjh@example.com")]
        [InlineData("<mailto:phil@example.co.uk|phil@example.co.uk>", "phil@example.co.uk")]
        public async Task SavesEmailAddress(string emailArgs, string expected)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var messageContext = env.CreateFakeMessageContext("my", $"email is {emailArgs}");
            var skill = env.Activate<MySkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(expected, user.Email);
            var log = await env.Db.AuditEvents.LastAsync();
            Assert.Equal("Set their email via the `my` skill.", log.Description);
        }
    }

    public class TheMyCommandWithTimeZoneProperty
    {
        [Fact]
        public async Task RepliesThatSlackProvidesTimeZone()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("my", "timezone is America/Los_Angeles");
            var skill = env.Activate<MySkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "I get your timezone from Slack, so that’s the best place to change it.",
                messageContext.SingleReply());
        }
    }

    public class TheMyCommandWithLocationProperty
    {
        [Fact]
        public async Task ReportsUnknownLocation()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var messageContext = env.CreateFakeMessageContext("my", "location");
            var skill = env.Activate<MySkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var response = messageContext.SingleReply();
            Assert.Equal($"I do not know your location. Try `<@{platformBotUserId}> my location is {{address or zip}}` to tell me your location.", response);
        }
    }
}

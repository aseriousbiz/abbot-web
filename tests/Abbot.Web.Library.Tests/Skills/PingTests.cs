using Serious.Abbot.Configuration;
using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class PingSkillTests
{
    public class TheOnMessageActivityAsyncMethod
    {
        [Fact]
        public async Task ReturnsPong()
        {
            var skill = new PingSkill(new FakeOptions<AbbotOptions>(new AbbotOptions { StaffOrganizationId = "staff" }));
            var message = FakeMessageContext.Create("ping", "");

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            var reply = message.SentActivities.Single();
            var attachment = reply.Attachments[0];
            Assert.StartsWith("Pong!", reply.Text);
            Assert.Equal("Pong", attachment.Name);
            Assert.Equal("image/gif", attachment.ContentType);
        }
    }
}

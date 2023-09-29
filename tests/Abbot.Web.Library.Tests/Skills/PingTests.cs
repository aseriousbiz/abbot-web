using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Skills;
using Serious.TestHelpers;
using Xunit;

public class PingSkillTests
{
    public class TheOnMessageActivityAsyncMethod
    {
        [Fact]
        public async Task ReturnsPong()
        {
            var skill = new PingSkill();
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

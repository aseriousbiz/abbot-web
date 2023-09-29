using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Skills;
using Serious.Slack.BotFramework.Model;
using Xunit;

public class ForgetSkillTests
{
    public class TheForgetCommandByItself
    {
        [Fact]
        public async Task ReturnsUsagePattern()
        {
            var env = TestEnvironment.Create();
            var botUserId = env.TestData.Organization.PlatformBotUserId;
            var messageContext = env.CreateFakeMessageContext("forget");
            var forgetSkill = env.Activate<ForgetSkill>();

            await forgetSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();

            Assert.Contains($@"`<@{botUserId}> forget {{phrase}}` _forgets {{phrase}}", reply);
        }
    }

    public class TheForgetCommandJustKey
    {
        [Fact]
        public async Task RespondsWithPrompt()
        {
            var env = TestEnvironment.Create();
            await env.CreateMemoryAsync("haack", "unhelpful");
            var messageContext = env.CreateFakeMessageContext("forget", "haack");
            var forgetSkill = env.Activate<ForgetSkill>();

            await forgetSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleActivityReply();
            Assert.Equal($@"Are you sure you want to forget `haack`?", reply.Text);
            Assert.IsType<RichActivity>(reply);
        }

        [Fact]
        public async Task WithForceFlagForgetsTheValue()
        {
            var env = TestEnvironment.Create();
            await env.CreateMemoryAsync("haack", "unhelpful");
            var messageContext = env.CreateFakeMessageContext("forget", "haack --force");
            var forgetSkill = env.Activate<ForgetSkill>();

            await forgetSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal(@"I forgot `haack`.", reply);
        }

        [Fact]
        public async Task WithIgnoreFlagDoesNotForgetTheValue()
        {
            var env = TestEnvironment.Create();
            await env.CreateMemoryAsync("haack", "unhelpful");
            var messageContext = env.CreateFakeMessageContext("forget", "haack --ignore");
            var forgetSkill = env.Activate<ForgetSkill>();

            await forgetSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal(@"I won’t forget `haack`.", reply);
        }

        [Fact]
        public async Task ReportsThatItDoesNotKnowAboutUnknownValue()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("forget", "haack");
            var forgetSkill = env.Activate<ForgetSkill>();

            await forgetSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal(@"Nothing to forget. I don’t know anything about `haack`.", reply);
        }
    }
}

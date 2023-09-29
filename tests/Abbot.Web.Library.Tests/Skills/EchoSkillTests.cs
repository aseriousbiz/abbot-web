using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class EchoSkillTests
{
    public class TheEchoCommand
    {
        [Fact]
        public async Task ReturnsUsagePattern()
        {
            var skill = new EchoSkill();
            var message = FakeMessageContext.Create("echo", "");

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                @"`<@U001> echo {phrase}` _responds with {phrase}._",
                message.SingleReply());
        }

        [Fact]
        public async Task RespondsWithTheGivenText()
        {
            var skill = new EchoSkill();
            var message = FakeMessageContext.Create("echo", "hello!");

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("hello!", message.SingleReply());
        }

        [Fact]
        public async Task RespondsWithMultilineText()
        {
            var skill = new EchoSkill();
            var message = FakeMessageContext.Create("echo", "hello\nworld!");

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("hello\nworld!", message.SingleReply());
        }

        [Fact]
        public async Task RespondsWithTheSpecifiedFormatSet()
        {
            var skill = new EchoSkill();
            var message = FakeMessageContext.Create("echo", "format:plain `hello!`");

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            var reply = message.SentActivities.Single();
            Assert.Equal("`hello!`", reply.Text);
            Assert.Equal("plain", reply.TextFormat);
        }
    }
}

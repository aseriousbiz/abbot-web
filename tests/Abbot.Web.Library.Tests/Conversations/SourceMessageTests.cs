using Serious.Abbot.AI;
using Serious.Cryptography;
using Serious.Slack;
using Serious.TestHelpers;

public class SourceMessageTests
{
    public class TheConstructor
    {
        public TheConstructor()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
        }

        [Fact]
        public void ReliesOnTokenUsageForPromptCount()
        {
            var message = new SourceMessage(
                "Test",
                null,
                SlackTimestamp.Parse("1681253218.828619"),
                new CompletionInfo("Test", new TokenUsage(2, PromptTokenCount: 420, 5)));

            Assert.Equal(420, message.PromptTokenCount);
        }

        [Fact]
        public void CalculatesPromptTokenCountWhenSummaryInfoIsNull()
        {
            var message = new SourceMessage(
                "This string has eleven tokens!",
                null,
                SlackTimestamp.Parse("1681253218.828619"));

            Assert.Equal(11, message.PromptTokenCount);
        }
    }
}

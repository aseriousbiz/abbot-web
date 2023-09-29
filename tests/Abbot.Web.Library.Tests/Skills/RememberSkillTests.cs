using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Skills;
using Xunit;

public class RememberSkillTests
{
    public class TheRemCommandByItself
    {
        [Fact]
        public async Task ReturnsUsagePattern()
        {
            var env = TestEnvironment.Create();
            var botUserId = env.TestData.Organization.PlatformBotUserId;
            var messageContext = env.CreateFakeMessageContext("rem", string.Empty);
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Contains($@"`<@{botUserId}> rem {{some phrase}} is {{some value}}`", reply);
        }
    }

    public class TheRemCommandWithKeyAndValue
    {
        [Theory]
        [InlineData("search is cool", @"Ok! I will remember that `search` is `cool`.")]
        [InlineData("haack is unhelpful", @"Ok! I will remember that `haack` is `unhelpful`.")]
        [InlineData("haack   is    unhelpful", @"Ok! I will remember that `haack` is `unhelpful`.")]
        [InlineData("`what is` is a good question", @"Ok! I will remember that ``what is`` is `a good question`.")]
        [InlineData("style guide is <https://ab.bot/help/styleguide|https://ab.bot/help/styleguide>", @"Ok! I will remember that `style guide` is `https://ab.bot/help/styleguide`.")]
        public async Task AddsItemAndReportsBack(string arguments, string expectedReply)
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("rem", arguments);
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal(expectedReply, reply);
        }

        [Theory]
        [InlineData("haack = unhelpful")]
        [InlineData("haack is unhelpful")]
        [InlineData("haack  is  unhelpful")]
        public async Task ReportsThatItAlreadyRemembersSomething(string arguments)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            await env.CreateMemoryAsync("haack", "unhelpful");
            var messageContext = env.CreateFakeMessageContext("rem", arguments);
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.StartsWith($@"`haack` is already `unhelpful` (added by <@{user.PlatformUserId}> on ", reply);
        }
    }

    public class TheRemCommandJustKey
    {
        [Fact]
        public async Task RetrievesTheValue()
        {
            var env = TestEnvironment.Create();
            await env.CreateMemoryAsync("haack", "unhelpful");
            var messageContext = env.CreateFakeMessageContext("rem", "haack");
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal("unhelpful", reply);
        }

        [Theory]
        [InlineData("haack")]
        [InlineData("search haack")]
        public async Task ReportsThatItDoesNotKnowAboutUnknownValue(string arguments)
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("rem", arguments);
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal($@"I couldn’t find anything about `{arguments}`.", reply);
        }

        [Fact]
        public async Task ReturnsSearchResultsForUnknownValue()
        {
            var env = TestEnvironment.Create();
            var itemsToRemember = new[]
            {
                "abbot is antagonistic",
                "haack's address is home",
                "paul's address is where the heart is"
            };
            // Seed some items.
            var skill = env.Activate<RememberSkill>();
            foreach (var item in itemsToRemember)
            {
                var seedMessage = env.CreateFakeMessageContext("rem", item);
                await skill.OnMessageActivityAsync(seedMessage, CancellationToken.None);
            }
            var messageContext = env.CreateFakeMessageContext("rem", "address");

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal(@"I don’t know anything about `address`, but I found these similar items:
• `haack's address`
• `paul's address`", reply);
        }
    }

    public class TheRemSearchCommand
    {
        [Theory]
        [InlineData("| address", "address")]
        [InlineData("| is address", "is address")]
        public async Task ReturnsMessageWhenItCannotFindSearchTerm(string arguments, string expectedSearchTerm)
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("rem", arguments);
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal($@"I couldn’t find anything about `{expectedSearchTerm}`.", reply);
        }

        [Fact]
        public async Task ReturnsMatchingRemKeys()
        {
            var env = TestEnvironment.Create();
            await env.CreateMemoryAsync("abbot", "is antagonistic");
            await env.CreateMemoryAsync("haack's address", "is home");
            await env.CreateMemoryAsync("paul's address", "is where the heart is");
            var messageContext = env.CreateFakeMessageContext("rem", "| address");
            var skill = env.Activate<RememberSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SingleReply();
            Assert.Equal(@"Search results:
• `haack's address`
• `paul's address`", reply);
        }
    }
}

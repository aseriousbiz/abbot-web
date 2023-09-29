using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;
using Xunit;

public class UsageBuilderTests
{
    public class TheAddMethod
    {
        [Fact]
        public void AddsUsageExampleNoDescriptionSlack()
        {
            var usageBuilder = UsageBuilder.Create(
                "skill",
                new SlackBotChannelUser("T001", "B001", "U001", "abbot"));

            usageBuilder.Add("add {name}", "");

            var result = usageBuilder.ToString();
            Assert.Equal("`<@U001> skill add {name}`.", result);
        }

        [Fact]
        public void AddsUsageExampleAndDescriptionSlack()
        {
            var usageBuilder = UsageBuilder.Create(
                "skill",
                new SlackBotChannelUser("T001", "B001", "U001", "abbot"));

            usageBuilder.Add("add {name}", "Adds {name}");

            var result = usageBuilder.ToString();
            Assert.Equal("`<@U001> skill add {name}` _Adds {name}_.", result);
        }
    }

    public class TheAddEmptyArgsUsageMethod
    {
        [Fact]
        public void AddsSlackUsageExampleAndDescriptionForNoArgs()
        {
            var usageBuilder = UsageBuilder.Create(
                "skill",
                new SlackBotChannelUser("T001", "B001", "U001", "abbot"));

            usageBuilder.AddEmptyArgsUsage("Shows usage");

            var result = usageBuilder.ToString();
            Assert.Equal("`<@U001> skill` _Shows usage_.", result);
        }

        [Fact]
        public void AddsSlackUsageExampleAndDescriptionForNoArgsWhenBotUserIdNotKnown()
        {
            var usageBuilder = UsageBuilder.Create(
                "skill",
                new SlackPartialBotChannelUser("T001", "B001", "abbot"));

            usageBuilder.AddEmptyArgsUsage("Shows usage");

            var result = usageBuilder.ToString();
            Assert.Equal("`@abbot skill` _Shows usage_.", result);
        }
    }

    public class TheAddExampleMethod
    {
        [Fact]
        public void PrefixesUsageWithExampleForSlack()
        {
            var usageBuilder = UsageBuilder.Create(
                "pug",
                new SlackBotChannelUser("T001", "B001", "U001", "abbot"));

            usageBuilder.AddExample("bomb", "Retrieves pugs");

            var result = usageBuilder.ToString();
            Assert.Equal("Ex. `<@U001> pug bomb` _Retrieves pugs_.", result);
        }
    }

    public class TheAddVerbatimMethod
    {
        [Fact]
        public void AddsVerbatimLine()
        {
            var usageBuilder = UsageBuilder.Create(
                "skill",
                new SlackBotChannelUser("T001", "B001", "U001", "abbot"));

            usageBuilder.AddVerbatim("This is verbatim");

            var result = usageBuilder.ToString();
            Assert.Equal("_This is verbatim_.", result);
        }
    }
}

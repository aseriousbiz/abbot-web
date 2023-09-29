using Abbot.Common.TestHelpers;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class HelpSkillTests
{
    public class WithNoArguments
    {
        [Fact]
        public async Task ReturnsHelpOnAbbot()
        {
            var env = TestEnvironment.CreateWithoutData();
            env.BuiltinSkillRegistry.AddSkills(new ISkill[]
            {
                new UnpronounceableSkill(),
                new FancySkill(),
                new SkillWithDescription(),
                new AnotherSkillWithDescription()
            });
            var message = FakeMessageContext.Create("help", "");
            var helpSkill = env.Activate<HelpSkill>();

            await helpSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.EndsWith(@"To get help for a specific skill, call `<@U001> help {skill}`. To see a list of all skills, call `<@U001> skills`. To create or install skills, or otherwise manage your Abbot, visit https://app.ab.bot/.",
                message.SingleReply());
        }
    }

    public class WithSkillName
    {
        [Fact]
        public async Task ShowsHelpAndUsageForThatSkill()
        {
            var env = TestEnvironment.CreateWithoutData();
            env.BuiltinSkillRegistry.AddSkill(new SkillWithHelpAndDescription());
            var message = FakeMessageContext.Create("help", "helpful");
            var helpSkill = env.Activate<HelpSkill>();

            await helpSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(@"My skill is very helpful.
Usage:
`<@U001> helpful is helping` _helps the helping_.", message.SingleReply());
        }

        [Fact]
        public async Task ShowsHelpForContainerSkillWhenSkillMatchesContainerSkillName()
        {
            var env = TestEnvironment.CreateWithoutData();
            env.BuiltinSkillRegistry.AddSkill(new SkillContainer());
            var message = FakeMessageContext.Create("help", "parameter");
            var helpSkill = env.Activate<HelpSkill>();

            await helpSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "(_no description_)\nUsage:\n`<@U001> parameter parent-skill` _does foo_.",
                message.SingleReply());
        }

        [Fact]
        public async Task ShowsHelpForSkillWhenSkillNameDoesNotMatchContainerSkillName()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkill(new SkillContainer());
            var organization = env.TestData.Organization;
            await env.CreateAliasAsync("translate", "parameter", "");
            var message = env.CreateFakeMessageContext("help", "translate");
            var helpSkill = env.Activate<HelpSkill>();

            await helpSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"(_no description_)\nUsage:\n`<@{organization.PlatformBotUserId}> translate sub-skill` _does stuff_.",
                message.SingleReply());
        }

        [Fact]
        public async Task ShowsHelpWithoutUsageForSkillWithoutUsage()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkill(new SkillWithHelp());
            var message = FakeMessageContext.Create("help", "helpful");
            var helpSkill = env.Activate<HelpSkill>();

            await helpSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("My skill is very helpful.", message.SingleReply());
        }

        [Fact]
        public async Task ShowsNotFoundMessageWhenSkillNotFound()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkills(new ISkill[]
            {
                new UnpronounceableSkill(),
                new FancySkill(),
                new SkillWithDescription(),
                new AnotherSkillWithDescription()
            });
            var message = FakeMessageContext.Create("help", "this-do-not-exist");
            var helpSkill = env.Activate<HelpSkill>();


            await helpSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(@"The skill `this-do-not-exist` does not exist.", message.SingleReply());
        }
    }

    class FancySkill : HelpSkillTestSkill
    {
    }

    [Skill("random")]
    class UnpronounceableSkill : HelpSkillTestSkill
    {
    }

    [Skill("test", Description = "A skill for a unit test")]
    class SkillWithDescription : HelpSkillTestSkill
    {
    }

    [Skill("awkward", Description = "My skill is saying the most awkward thing in every situation.")]
    class AnotherSkillWithDescription : HelpSkillTestSkill
    {
    }

    [Skill("helpful", Description = "My skill is very helpful.")]
    class SkillWithHelpAndDescription : HelpSkillTestSkill
    {
        public override void BuildUsageHelp(UsageBuilder usage)
        {
            usage.Add("is helping", "helps the helping");
        }
    }

    [Skill("parameter")]
    class SkillContainer : HelpSkillTestSkill, ISkillContainer
    {
        public override void BuildUsageHelp(UsageBuilder usage)
        {
            usage.Add("parent-skill", "does foo");
        }

        public void BuildSkillUsageHelp(UsageBuilder usage)
        {
            usage.Add("sub-skill", "does stuff");
        }
    }

    [Skill("helpful", Description = "My skill is very helpful.")]
    class SkillWithHelp : HelpSkillTestSkill
    {
    }

    class HelpSkillTestSkill : ISkill
    {
        public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public virtual void BuildUsageHelp(UsageBuilder usage)
        {
        }
    }
}

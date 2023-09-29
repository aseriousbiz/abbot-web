using System;
using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;
using Serious.TestHelpers;
using Xunit;

public class ListSkillTests
{
    public class WithNoArguments
    {
        [Fact]
        public async Task ReturnsListOfSkillsInAlphabeticOrder()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkills(
                new UnpronounceableSkill(),
                new FancySkill(),
                new SkillWithDescription(),
                new AnotherSkillWithDescription()
            );
            var message = FakeMessageContext.Create("help", "", organization: env.TestData.Organization);
            var skill = env.Activate<SkillsSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(@"• `awkward` - My skill is saying the most awkward thing in every situation.
• `fancy`
• `joke` - Abbot's favorite jokes.
• `random`
• `remember` - A shortcut to `rem`.
• `test` - A skill for a unit test.",
                message.SingleReply());
        }
    }

    public class WithSearchPattern
    {
        [Fact]
        public async Task ShowsListOfMatchingSkills()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkills(
                new UnpronounceableSkill(),
                new RandoSkill(),
                new SkillWithDescription(),
                new AnotherSkillWithDescription(),
                new RandomizeSkill()
            );
            var message = env.CreateFakeMessageContext("help", "| rand");
            var skill = env.Activate<SkillsSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "• `asdf` - Another rando skill.\n• `rando` - A skill for randos.\n• `random`",
                message.SingleReply());
        }

        [Fact]
        public async Task ShowsMessageWhenNothingFound()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkills(
                new UnpronounceableSkill(),
                new RandoSkill(),
                new SkillWithDescription(),
                new AnotherSkillWithDescription(),
                new RandomizeSkill()
            );
            var message = env.CreateFakeMessageContext("help", "| aCommandThatDoesNotExist");
            var skill = env.Activate<SkillsSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "I did not find any skills similar to `aCommandThatDoesNotExist`",
                message.SingleReply());
        }
    }

    class FancySkill : HelpSkillTestSkill
    {
    }

    [Skill("random")]
    class UnpronounceableSkill : HelpSkillTestSkill
    {
    }

    [Skill("rando", Description = "A skill for randos")]
    class RandoSkill : HelpSkillTestSkill
    {
    }

    [Skill("asdf", Description = "Another rando skill")]
    class RandomizeSkill : HelpSkillTestSkill
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

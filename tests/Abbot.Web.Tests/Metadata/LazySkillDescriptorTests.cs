using System;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Messaging;
using Serious.Abbot.Metadata;
using Serious.Abbot.Skills;
using Xunit;

public class BuiltinSkillDescriptorTests
{
    public class TheConstructor
    {
        [Fact]
        public void SetsDescriptionAndNormalizedNameFromSkillAttribute()
        {
            var descriptor = new BuiltinSkillDescriptor(typeof(SomeSkill), new Lazy<ISkill>(() => new SomeSkill()));

            Assert.Equal("something", descriptor.Name);
            Assert.Equal("A skill for this unit test", descriptor.Description);
        }

        [Fact]
        public void SetsDescriptionAndInfersNameFromSkillAttributeWithDescriptionButNoName()
        {
            var descriptor = new BuiltinSkillDescriptor(typeof(SkillWithInferredName), new Lazy<ISkill>(() => new SkillWithInferredName()));

            Assert.Equal("skillwithinferredname", descriptor.Name);
            Assert.Equal("A test skill where the name should still be inferred from the type.", descriptor.Description);
        }

        [Fact]
        public void ThrowsArgumentExceptionWhenTypeDoesNotImplementISkill()
        {
            Assert.Throws<ArgumentException>(() =>
                new BuiltinSkillDescriptor(typeof(NotASkill), new Lazy<ISkill>(() => new SomeSkill())));
        }

        [Skill("SOMETHING", Description = "A skill for this unit test")]
        class SomeSkill : LazySkill
        {
        }

        [Skill("NADA", Description = "This is not a skill")]
        class NotASkill
        {
        }

        [Skill(Description = "A test skill where the name should still be inferred from the type.")]
        class SkillWithInferredName : LazySkill
        {
        }

        class LazySkill : ISkill
        {
            public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public void BuildUsageHelp(UsageBuilder usage)
            {
            }
        }
    }
}

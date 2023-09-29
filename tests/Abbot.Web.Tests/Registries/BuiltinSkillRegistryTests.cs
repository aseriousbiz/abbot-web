using System;
using Serious.Abbot.Metadata;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.TestHelpers;
using Xunit;

public class BuiltinSkillRegistryTests
{
    public class TheIndexer
    {
        [Fact]
        public void ReturnsSkillByKeyCaseInsensitively()
        {
            var descriptors = new[]
            {
                new BuiltinSkillDescriptor(typeof(EchoSkill), new Lazy<ISkill>(() => new EchoSkill())),
                new BuiltinSkillDescriptor(typeof(FakeSkill), new Lazy<ISkill>(() => new FakeSkill("FAKE")))
            };
            var registry = new BuiltinSkillRegistry(descriptors);

            var echoSkill = registry["echo"];

            Assert.NotNull(echoSkill);
            Assert.Same(echoSkill, registry["ECHO"]);
            Assert.NotNull(registry["FAKE"]);
            Assert.NotSame(echoSkill, registry["FAKE"]);
        }
    }
}

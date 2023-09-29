using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Templating;

public class AISettingsRegistryTests
{
    public class TheGetModelSettingsAsyncMethod
    {
        public static IEnumerable<object[]> AllAIFeatures()
        {
            return Enum.GetValues<AIFeature>()
                .Select(f => new object[] { f });
        }

        [Theory]
        [MemberData(nameof(AllAIFeatures))]
        public async Task GetsDefaultSettings(
            AIFeature feature)
        {
            var env = TestEnvironment.Create();
            var expectedDefault = AISettingsRegistry.Defaults[feature];
            var registry = env.Activate<AISettingsRegistry>();

            var settings = await registry.GetModelSettingsAsync(feature);

            Assert.Equal(expectedDefault, settings);
        }

        [Theory]
        [MemberData(nameof(AllAIFeatures))]
        public async Task CanReadV1ModelSettings(AIFeature feature)
        {
            var env = TestEnvironment.Create();
            await env.Settings.SetAsync(
                SettingsScope.Global,
                $"AI:Model:Settings:{feature}",
                """{"Model":"gpt-4","PromptTemplate":"Prompty","Temperature":1.0}""",
                env.TestData.User);
            var registry = env.Activate<AISettingsRegistry>();

            var settings = await registry.GetModelSettingsAsync(feature);
            Assert.Equal("Prompty", settings.Prompt.Text);
            Assert.Equal(PromptVersion.Version1, settings.Prompt.Version);
            Assert.Equal(1.0, settings.Temperature);
            Assert.Equal("gpt-4", settings.Model);
        }
    }
}

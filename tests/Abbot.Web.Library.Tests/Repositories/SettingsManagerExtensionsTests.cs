using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Xunit;

public class SettingsManagerExtensionsTests
{
    public class TheGetBooleanValueMethod
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReturnsDefaultValueIfNotPresent(bool defaultIfNull)
        {
            var env = TestEnvironment.Create();
            var settings = env.Activate<SettingsManager>();

            var result = await settings.GetBooleanValueAsync(SettingsScope.Global, "missing", defaultIfNull);

            Assert.Equal(defaultIfNull, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReturnsSetValue(bool valueToSet)
        {
            var env = TestEnvironment.Create();
            var settings = env.Activate<SettingsManager>();
            await settings.SetBooleanValueWithAuditing(
                SettingsScope.Global,
                "key",
                valueToSet,
                env.TestData.User,
                env.TestData.Organization);

            var result = await settings.GetBooleanValueAsync(SettingsScope.Global, "key", true);

            Assert.Equal(valueToSet, result);
        }
    }
}

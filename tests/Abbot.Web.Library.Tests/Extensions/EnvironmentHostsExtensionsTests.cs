
using Serious.Abbot.Helpers;
using Xunit;

public class EnvironmentHostsExtensionsTests
{
    public class TheParseAllowedHostsMethod
    {
        [Theory]
        [InlineData("localhost,app.ab.bot,in.ab.bot")]
        [InlineData("localhost,app.ab.bot,in.ab.bot,")]
        [InlineData("localhost, app.ab.bot, in.ab.bot")]
        public void ParsesCommaDelimitedListOfHosts(string value)
        {
            var result = value.ParseAllowedHosts();

            Assert.Collection(result,
                h => Assert.Equal("localhost", h),
                h => Assert.Equal("app.ab.bot", h),
                h => Assert.Equal("in.ab.bot", h));
        }

        [Fact]
        public void ReturnsEmptyArrayWhenNull()
        {
            var result = ((string?)null).ParseAllowedHosts();

            Assert.Empty(result);
        }


        [Fact]
        public void DoesNotOverrideDefaults()
        {
            var defaults = new[] { "localhost", "app.ab.bot", "in.ab.bot" };

            var result = "api.ab.bot,run.ab.bot".ParseAllowedHosts(defaults);

            Assert.Collection(result,
                h => Assert.Equal("api.ab.bot", h),
                h => Assert.Equal("run.ab.bot", h));
        }

        [Fact]
        public void FallsBackToDefaultsWhenNull()
        {
            var defaults = new[] { "localhost", "app.ab.bot", "in.ab.bot" };

            var result = ((string?)null).ParseAllowedHosts(defaults);

            Assert.Collection(result,
                h => Assert.Equal("localhost", h),
                h => Assert.Equal("app.ab.bot", h),
                h => Assert.Equal("in.ab.bot", h));
        }
    }
}

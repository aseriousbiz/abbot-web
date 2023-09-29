using Serious.Abbot.Functions.Services;
using Serious.TestHelpers;
using Xunit;

public class EnvironmentExtensionsTests
{
    public class TheGetAbbotReplyUrlMethod
    {
        [Theory]
        [InlineData("https://ab.bot/api", "https://ab.bot/api/skills/123/reply")]
        [InlineData("https://ab.bot/api/", "https://ab.bot/api/skills/123/reply")]
        [InlineData("https://localhost/", "https://localhost/skills/123/reply")]
        [InlineData("https://localhost", "https://localhost/skills/123/reply")]
        public void ReturnsTheAbbotReplyUrl(string apiBaseUrl, string expected)
        {
            var environment = new FakeEnvironment();
            environment.Add("AbbotApiBaseUrl", apiBaseUrl);

            var result = environment.GetAbbotReplyUrl(123);

            Assert.Equal(expected, result.ToString());
        }
    }

    public class TheGetSkillApiUrlMethod
    {
        [Theory]
        [InlineData("https://ab.bot/api", "https://ab.bot/api/skills/42")]
        [InlineData("https://ab.bot/api/", "https://ab.bot/api/skills/42")]
        [InlineData("https://localhost/", "https://localhost/skills/42")]
        [InlineData("https://localhost", "https://localhost/skills/42")]
        public void ReturnsTheAbbotReplyUrl(string apiBaseUrl, string expected)
        {
            var environment = new FakeEnvironment();
            environment.Add("AbbotApiBaseUrl", apiBaseUrl);

            var result = environment.GetSkillApiUrl(42);

            Assert.Equal(expected, result.ToString());
        }

        [Fact]
        public void UsesMoreSpecificUrl()
        {
            var environment = new FakeEnvironment();
            environment.Add("AbbotApiBaseUrl", "https://whatevs");
            environment.Add("SkillApiBaseUriFormatString", "https://ab.bot/api/skills/{0}");

            var result = environment.GetSkillApiUrl(42);

            Assert.Equal("https://ab.bot/api/skills/42", result.ToString());
        }
    }
}

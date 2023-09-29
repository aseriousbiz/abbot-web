using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;
using Serious.Slack.AspNetCore;
using Serious.TestHelpers;
using Xunit;

public class PlatformRequirementsTests
{
    public class TheHasRequiredScopesMethod
    {
        [Theory]
        [InlineData(null, new[] { "app_mention", "channels:history" })]
        [InlineData("A12", new[] { "app_mention", "channels:history" })]
        [InlineData("A34", new[] { "app_mention", "files:write" })]
        public void ReturnsTrueWhenSlackPlatformHasNullScopes(string? botAppId, string[] missingScopes)
        {
            var organization = new Organization
            {
                BotAppId = botAppId,
                PlatformType = PlatformType.Slack,
                Scopes = null
            };
            var options = Options.Create(new SlackOptions
            {
                AppId = "A12",
                RequiredScopes = "app_mention,channels:history",
                CustomAppScopes = "app_mention,files:write",
            });
            var requirements = new PlatformRequirements(options);

            Assert.True(requirements.HasRequiredScopes(organization));

            Assert.Equal(missingScopes, requirements.MissingScopes(organization));
        }

        [Theory]
        [InlineData(null, "app_mention,channels:history")]
        [InlineData(null, "channels:history,app_mention,more")]
        [InlineData("A12", "app_mention,channels:history")]
        [InlineData("A12", "channels:history,app_mention,more")]
        [InlineData("A34", "app_mention,files:write")]
        [InlineData("A34", "files:write,app_mention,more")]
        public void ReturnsTrueWhenSlackPlatformHasRequiredScopesOrMore(string? botAppId, string? orgScopes)
        {
            var organization = new Organization
            {
                BotAppId = botAppId,
                PlatformType = PlatformType.Slack,
                Scopes = orgScopes,
            };
            var options = Options.Create(new SlackOptions
            {
                AppId = "A12",
                RequiredScopes = "app_mention,channels:history",
                CustomAppScopes = "app_mention,files:write",
            });
            var requirements = new PlatformRequirements(options);

            var result = requirements.HasRequiredScopes(organization);

            Assert.True(result);
            Assert.Empty(requirements.MissingScopes(organization));
        }

        [Theory]
        [InlineData(null, "app_mention", new[] { "channels:history" })]
        [InlineData(null, "channels:history", new[] { "app_mention" })]
        [InlineData("A12", "app_mention", new[] { "channels:history" })]
        [InlineData("A12", "channels:history", new[] { "app_mention" })]
        [InlineData("A34", "app_mention", new[] { "channels:history", "files:write" })]
        [InlineData("A34", "app_mention,channels:history", new[] { "files:write" })]
        [InlineData("A34", "channels:history,files:write", new[] { "app_mention" })]
        public void ReturnsFalseWhenSlackPlatformLacksARequiredScope(
            string? botAppId,
            string? orgScopes,
            string[] missingScopes)
        {
            var organization = new Organization
            {
                BotAppId = botAppId,
                PlatformType = PlatformType.Slack,
                Scopes = orgScopes
            };
            var options = Options.Create(new SlackOptions
            {
                AppId = "A12",
                RequiredScopes = "app_mention,channels:history",
                CustomAppScopes = "app_mention,channels:history,files:write",
            });
            var requirements = new PlatformRequirements(options);

            var result = requirements.HasRequiredScopes(organization);

            Assert.False(result);

            Assert.Equal(missingScopes, requirements.MissingScopes(organization));
        }
    }
}

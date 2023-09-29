using Microsoft.IdentityModel.Tokens;
using Serious.Abbot.Entities;

public class PlaybookExtensionTests
{
    public class TheGetWebhookTriggerTokenMethod
    {
        [Fact]
        public void GeneratesTokenWithOrgIdEmbedded()
        {
            var playbook = new Playbook
            {
                OrganizationId = 42,
                Properties = new PlaybookProperties()
                {
                    WebhookTokenSeed = "The Seed"
                },
                Name = "Test Playbook",
                Slug = "hut-hut-hut",
                Enabled = true,
            };

            var token = playbook.GetWebhookTriggerToken();

            Assert.Equal("U3lzdGVtLkJ5dGVbXTo0Mg", token);
            var decoded = Base64UrlEncoder.Decode(token);
            Assert.Equal(42, int.Parse(decoded.Split(':')[1]));
        }
    }

    public class TheTryGetOrganizationIdFromToken
    {
        [Theory]
        [InlineData("U3lzdGVtLkJ5dGVbXTo0Mg", true, 42)]
        [InlineData("garbage", false, 0)]
        [InlineData("U3lzdGVtLkJ5dGVbXTox", true, 1)]
        public void CanGetOrganizationIdFromToken(string token, bool expectedResult, int expectedOrganizationId)
        {
            var result = PlaybookExtensions.TryGetOrganizationIdFromToken(token, out var organizationId);

            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedOrganizationId, organizationId);
        }
    }

    public class TheIsValidWebhookTriggerTokenMethod
    {
        [Fact]
        public void CanValidateToken()
        {
            var playbook = new Playbook
            {
                OrganizationId = 42,
                Properties = new PlaybookProperties()
                {
                    WebhookTokenSeed = "The Seed"
                },
                Name = "Test Playbook",
                Slug = "hut-hut-hut",
                Enabled = true,
            };

            var result = playbook.IsValidWebhookTriggerToken("U3lzdGVtLkJ5dGVbXTo0Mg");

            Assert.True(result);
        }
    }
}

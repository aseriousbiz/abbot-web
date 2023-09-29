using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Storage;
using Serious.TestHelpers;
using Xunit;

public class BotSecretsTests
{
    public class TheGetSecretAsyncMethod
    {
        [Fact]
        public async Task ReturnsEmptyStringWhenSecretNotFound()
        {
            var apiClient = new FakeSkillApiClient(42);
            var message = new SkillRunnerInfo
            {
                SkillId = 42
            };
            var secrets = new BotSecrets(apiClient);

            var result = await secrets.GetAsync("non-existent");

            Assert.Empty(result);
        }

        [Fact]
        public async Task CanRetrieveSecretFromApi()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/secret?key=some%2Fsecret");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, new SkillSecretResponse
            {
                Secret = "everything is not as it seems"
            });
            var message = new SkillRunnerInfo
            {
                SkillId = 42
            };
            var secrets = new BotSecrets(apiClient);

            var result = await secrets.GetAsync("some/secret");

            Assert.Equal("everything is not as it seems", result);
        }
    }
}

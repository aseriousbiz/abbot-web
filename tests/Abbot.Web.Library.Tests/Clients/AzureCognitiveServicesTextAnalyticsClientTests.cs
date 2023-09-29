using Microsoft.Extensions.Options;
using Serious.Abbot.AI;

public class AzureCognitiveServicesTextAnalyticsClientTests
{
    public class TheRecognizePiiEntitiesAsyncMethod
    {
        [Fact]
        public async Task UsesFallbackSanitizationWhenNoClientConfigured()
        {
            var options = Options.Create(new CognitiveServicesOptions());
            var client = new AzureCognitiveServicesTextAnalyticsClient(options);

            var result = await client.RecognizePiiEntitiesAsync("hey phil@ab.bot here.");

            var sensitiveValue = Assert.Single(result);
            Assert.Equal("phil@ab.bot", sensitiveValue.Text);
            Assert.Equal(4, sensitiveValue.Offset);
            Assert.Equal(sensitiveValue.Text.Length, sensitiveValue.Length);
        }
    }
}

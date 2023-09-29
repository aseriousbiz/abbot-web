using System;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Messages;
using Serious.TestHelpers;
using Xunit;

public class PassiveBotReplyClientTests
{
    public class TheDidReplyProperty
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task IsTrueNoMatterTheDelay(int delay)
        {
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(new Uri("https://fake/api/reply"), new ProactiveBotMessageResponse(true));
            var client = new PassiveBotReplyClient();

            await client.SendReplyAsync("Hello world!", TimeSpan.FromSeconds(delay), null);

            Assert.True(client.DidReply);
            Assert.Single(client.Replies);
        }

        [Fact]
        public void IsFalseWhenNoMessagesSent()
        {
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(new Uri("https://fake/api/reply"), new ProactiveBotMessageResponse(true));
            var client = new PassiveBotReplyClient();

            Assert.False(client.DidReply);
            Assert.Empty(client.Replies);
        }
    }
}

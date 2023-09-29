using Newtonsoft.Json;
using Serious;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;
using Serious.TestHelpers;
using Xunit;

public class ActiveBotReplyClientTests
{
    public class TheSendReplyAsyncMethod
    {
        [Fact]
        public async Task UsesApiClientToSendReply()
        {
            var environment = new FakeEnvironment
            {
                { "AbbotApiBaseUrl", "https://fake/api" }
            };
            var apiClient = new FakeSkillApiClient(123);
            apiClient.AddResponse(new Uri("https://fake/api/skills/123/reply"), new ProactiveBotMessageResponse(true));
            var runnerInfo = new SkillRunnerInfo
            {
                SkillId = 123
            };
            var skillInfo = new SkillInfo
            {
                Room = new PlatformRoom("C0001234", "the-room")
            };
            var message = new SkillMessage { RunnerInfo = runnerInfo, SkillInfo = skillInfo };
            var apiContext = new SkillContext(message, "some-api-key");
            var contextAccessor = new SkillContextAccessor { SkillContext = apiContext };
            var client = new ActiveBotReplyClient(apiClient, environment, contextAccessor);

            await client.SendReplyAsync("Hello world!", TimeSpan.Zero, new MessageOptions
            {
                To = new MessageTarget(new ChatAddress(ChatAddressType.Room, "C0004321"))
            });

            var sentData = apiClient.SentJson[(new Uri("https://fake/api/skills/123/reply"), HttpMethod.Post)].Single();
            var sentMessage = Assert.IsType<ProactiveBotMessage>(sentData);
            Assert.NotNull(sentMessage.Options?.To);
            Assert.Equal(123, sentMessage.SkillId);
            Assert.Equal("Hello world!", sentMessage.Message);
            Assert.NotNull(sentMessage.Options);
            Assert.Equal(new ChatAddress(ChatAddressType.Room, "C0004321"), sentMessage.Options.To);
            Assert.True(client.DidReply);
        }
    }

    public class TheSendSlackReplyAsyncMethod
    {
        [Fact]
        public async Task UsesApiClientToSendReply()
        {
            var blocks = new ILayoutBlock[]
            {
                new Section
                {
                    Text = new MrkdwnText("test")
                }
            };
            var blocksJson = SlackSerializer.Serialize(blocks);
            var environment = new FakeEnvironment
            {
                {"AbbotApiBaseUrl", "https://fake/api"}
            };
            var apiClient = new FakeSkillApiClient(123);
            apiClient.AddResponse(new Uri("https://fake/api/skills/123/reply"), new ProactiveBotMessageResponse(true));
            var runnerInfo = new SkillRunnerInfo
            {
                SkillId = 123
            };
            var skillInfo = new SkillInfo
            {
                Room = new PlatformRoom("C0001234", "the-room")
            };
            var message = new SkillMessage { RunnerInfo = runnerInfo, SkillInfo = skillInfo };
            var apiContext = new SkillContext(message, "some-api-key");
            var contextAccessor = new SkillContextAccessor { SkillContext = apiContext };
            var client = new ActiveBotReplyClient(apiClient, environment, contextAccessor);

            await client.SendSlackReplyAsync("Hello world!", blocksJson, null);

            var sentData = apiClient.SentJson[(new Uri("https://fake/api/skills/123/reply"), HttpMethod.Post)].Single();
            var sentMessage = Assert.IsType<ProactiveBotMessage>(sentData);
            Assert.NotNull(sentMessage.Blocks);
            var receivedBlocks = JsonConvert.DeserializeObject<ILayoutBlock[]>(sentMessage.Blocks);
            Assert.NotNull(receivedBlocks);
            var block = Assert.Single(receivedBlocks);
            var section = Assert.IsType<Section>(block);
            Assert.NotNull(section.Text);
            Assert.Equal("test", section.Text.Text);
            Assert.True(client.DidReply);
        }
    }

    public class TheDidReplyProperty
    {
        [Fact]
        public async Task IsTrueIfAtLeastOneMessageWithNoDelaySent()
        {
            var environment = new FakeEnvironment
            {
                {"AbbotApiBaseUrl", "https://fake/api"}
            };
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(new Uri("https://fake/api/skills/42/reply"), new ProactiveBotMessageResponse(true));
            var runnerInfo = new SkillRunnerInfo
            {
                SkillId = 42
            };
            var skillInfo = new SkillInfo
            {
                Room = new PlatformRoom("C0001234", "the-room")
            };
            var message = new SkillMessage { RunnerInfo = runnerInfo, SkillInfo = skillInfo };
            var apiContext = new SkillContext(message, "some-api-key");
            var contextAccessor = new SkillContextAccessor { SkillContext = apiContext };
            var client = new ActiveBotReplyClient(apiClient, environment, contextAccessor);

            await client.SendReplyAsync("Hello world 1!", TimeSpan.FromDays(1), null);
            await client.SendReplyAsync("Hello world 2!", TimeSpan.Zero, null);
            await client.SendReplyAsync("Hello world 3!", TimeSpan.FromDays(1), null);

            Assert.True(client.DidReply);
        }

        [Fact]
        public async Task IsFalseWhenOnlyDelayedMessagesSent()
        {
            var environment = new FakeEnvironment
            {
                {"AbbotApiBaseUrl", "https://fake/api"}
            };
            var apiClient = new FakeSkillApiClient(123);
            apiClient.AddResponse(new Uri("https://fake/api/skills/123/reply"), new ProactiveBotMessageResponse(true));
            var runnerInfo = new SkillRunnerInfo
            {
                SkillId = 123
            };
            var skillInfo = new SkillInfo
            {
                Room = new PlatformRoom("C0001234", "the-room")
            };
            var message = new SkillMessage { RunnerInfo = runnerInfo, SkillInfo = skillInfo };
            var apiContext = new SkillContext(message, "some-api-key");
            var contextAccessor = new SkillContextAccessor { SkillContext = apiContext };
            var client = new ActiveBotReplyClient(apiClient, environment, contextAccessor);

            await client.SendReplyAsync("Hello world!", TimeSpan.FromDays(1), null);

            Assert.False(client.DidReply);
        }
    }
}

using System.Threading.Tasks;
using Newtonsoft.Json;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Messages;
using Serious.Slack.BlockKit;
using Serious.TestHelpers;
using Xunit;

public class SlackClientTests
{
    public class TheReplyAsyncMethod
    {
        [Fact]
        public async Task ConvertsObjectToJson()
        {
            var botReplyClient = new FakeBotReplyClient();
            var skillContextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(new SkillMessage(), "ApiKey")
            };
            var client = new SlackClient(botReplyClient, skillContextAccessor);

            await client.ReplyAsync("fallback text", new Section(new MrkdwnText("*text*")));

            var sent = Assert.Single(botReplyClient.SentReplies);
            Assert.Equal("fallback text", sent.Message);
            Assert.NotNull(sent.Blocks);
            var blocks = JsonConvert.DeserializeObject<ILayoutBlock[]>(sent.Blocks);
            Assert.NotNull(blocks);
            var section = Assert.IsType<Section>(blocks[0]);
            Assert.NotNull(section.Text);
            Assert.Equal("*text*", section.Text.Text);
        }

        [Fact]
        public async Task SendsBlocksAsArrayJson()
        {
            var botReplyClient = new FakeBotReplyClient();
            var skillContextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(new SkillMessage(), "ApiKey")
            };
            var client = new SlackClient(botReplyClient, skillContextAccessor);

            await client.ReplyAsync("fallback text",
                new Section(new MrkdwnText("*text*")),
                new Divider());

            var sent = Assert.Single(botReplyClient.SentReplies);
            Assert.NotNull(sent.Blocks);
            Assert.StartsWith("[", sent.Blocks);
            var blocks = JsonConvert.DeserializeObject<ILayoutBlock[]>(sent.Blocks);
            Assert.NotNull(blocks);
            Assert.Collection(blocks,
                b0 => Assert.IsType<Section>(b0),
                b1 => Assert.IsType<Divider>(b1));
        }
    }
}

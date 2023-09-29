using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;

public class MessageClassifierTests
{
    public class TheClassifyMessageAsyncMethod
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReturnsCategoriesForClassifiedMessage(bool azureOpenAIEnabled)
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };
            await env.Db.SaveChangesAsync();
            env.OpenAiClient.Enabled = azureOpenAIEnabled;
            env.OpenAiClient.PushCompletionResult(
                "[!category1:value1][!category2:value2]");
            env.Clock.TravelTo(new DateTime(2021, 09, 16));
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var classifier = env.Activate<MessageClassifier>();

            var result = await classifier.ClassifyMessageAsync(
                "This is a new message",
                Array.Empty<SensitiveValue>(),
                "1677880536.112979",
                room,
                env.TestData.Member,
                env.TestData.Organization);

            var categories = result?.Categories;
            Assert.NotNull(categories);
            Assert.Collection(categories,
                c => Assert.Equal("category1:value1", c.ToString()),
                c => Assert.Equal("category2:value2", c.ToString()));
        }

        [Fact]
        public async Task ReturnsCategoriesWhenResponseContainsThoughtAndAction()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };
            await env.Db.SaveChangesAsync();
            env.OpenAiClient.PushCompletionResult(
                """
[Thought]The message is definitely a category1 value.[/Thought]
[Action][!category1:value1][/Action]
[Thought]The message is definitely a category2 value.[/Thought]
[Action][!category2:value2][/Action]
""");
            env.Clock.TravelTo(new DateTime(2021, 09, 16));
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var classifier = env.Activate<MessageClassifier>();

            var result = await classifier.ClassifyMessageAsync(
                "This is a new message",
                Array.Empty<SensitiveValue>(),
                "1677880536.112979",
                room,
                env.TestData.Member,
                env.TestData.Organization);

            var categories = result?.Categories;
            Assert.NotNull(categories);
            Assert.Collection(categories,
                c => Assert.Equal("category1:value1", c.ToString()),
                c => Assert.Equal("category2:value2", c.ToString()));
        }
    }
}

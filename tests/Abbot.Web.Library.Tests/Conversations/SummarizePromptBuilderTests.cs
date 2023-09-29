using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Cryptography;
using Serious.Slack;

public class SummarizePromptBuilderTests
{
    public class TheBuildAsyncMethod
    {
        [Fact]
        public async Task SummarizesSingleSlackMessageProtectingSensitiveEmails()
        {
            var env = TestEnvironment.Create<CustomTestData>();
            var customer = env.TestData.ForeignMember;
            var messages = new SourceMessage[]
            {
                new(
                    "The first message.",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse("1676486431.793829")),
            };
            var history = new SanitizedConversationHistory(messages, new Dictionary<string, SecretString>());
            var builder = env.Activate<SummarizePromptBuilder>();

            var prompts = await builder.BuildAsync(history, env.TestData.Conversation);

            var expectedPrompt =
                """
                system: State: New

                user: <@Uforeign> (Customer) says: The first message.

                """;
            Assert.Equal(expectedPrompt, prompts.Messages.Format());
        }

        [Fact]
        public async Task SummarizesTwoSlackMessagesUsingExistingSummaryAsExample()
        {
            var env = TestEnvironment.Create<CustomTestData>();
            var conversation = env.TestData.Conversation;
            conversation.Summary = "This is the current summary. [!conclusion:foo bar baz]";
            await env.Db.SaveChangesAsync();
            var (customer, agent) = env.TestData.GetCustomerAndAgent();
            var messages = new SourceMessage[]
            {
                new(
                    "The first message.",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse("1676486431.000001"),
                    new CompletionInfo("This is the current summary. [!conclusion:foo bar baz]", new TokenUsage(1, 2, 3))),
                new(
                    "The second message.",
                    new SourceUser(agent.User.PlatformUserId, "Support Agent"),
                    SlackTimestamp.Parse("1676486431.000002")),
            };
            var history = new SanitizedConversationHistory(messages, new Dictionary<string, SecretString>());
            var builder = env.Activate<SummarizePromptBuilder>();

            var prompts = await builder.BuildAsync(history, env.TestData.Conversation);

            var expectedPrompt =
                """
                system: State: New

                user: <@Uforeign> (Customer) says: The first message.

                assistant: This is the current summary. [!conclusion:foo bar baz]

                user: <@Uhome> (Support Agent) says: The second message.

                """;
            Assert.Equal(expectedPrompt, prompts.Messages.Format());
        }

        [Fact]
        public async Task SummarizesMultipleSlackMessagesButOnlyHasPenultimateMessageSummaryAsExample()
        {
            var env = TestEnvironment.Create<CustomTestData>();
            var conversation = env.TestData.Conversation;
            conversation.Summary = "This is the current summary. [!conclusion:foo bar baz]";
            await env.Db.SaveChangesAsync();
            var (customer, agent) = env.TestData.GetCustomerAndAgent();
            var messages = new SourceMessage[]
            {
                new(
                    "The first message.",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse("1676486431.000001"),
                    new CompletionInfo("First Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "The second message.",
                    new SourceUser(agent.User.PlatformUserId, "Support Agent"),
                    SlackTimestamp.Parse("1676486431.000002"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "The third message.",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse("1676486431.000001"),
                    new CompletionInfo("Third Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "The fourth message.",
                    new SourceUser(agent.User.PlatformUserId, "Support Agent"),
                    SlackTimestamp.Parse("1676486431.000002"),
                    new CompletionInfo("Fourth Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "The final message.",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse("1676486431.000001")),

            };
            var history = new SanitizedConversationHistory(messages, new Dictionary<string, SecretString>());
            var builder = env.Activate<SummarizePromptBuilder>();

            var prompts = await builder.BuildAsync(history, env.TestData.Conversation);

            var expectedPrompt =
                """
                system: State: New

                user: <@Uforeign> (Customer) says: The first message.
                <@Uhome> (Support Agent) says: The second message.
                <@Uforeign> (Customer) says: The third message.
                <@Uhome> (Support Agent) says: The fourth message.

                assistant: This is the current summary. [!conclusion:foo bar baz]

                user: <@Uforeign> (Customer) says: The final message.

                """;
            Assert.Equal(expectedPrompt, prompts.Messages.Format());
        }
    }

    public class CustomTestData : CommonTestData
    {
        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            env.TestData.Organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };

            await env.Db.SaveChangesAsync();
            await env.AISettings.SetModelSettingsAsync(
                AIFeature.Summarization,
                new ModelSettings
                {
                    Model = "x",
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            State: {{Conversation.State}}
                            """,
                    },
                    Temperature = 1.0
                },
                env.TestData.Member);
            await base.SeedAsync(env);
            Room = await env.CreateRoomAsync();
            Conversation = await env.CreateConversationAsync(Room);
        }

        public Room Room { get; private set; } = null!;

        public Conversation Conversation { get; private set; } = null!;

        public (Member, Member) GetCustomerAndAgent() => (ForeignMember, Member);
    }

}

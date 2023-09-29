using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Cryptography;
using Serious.Slack;

public class SanitizedPromptMessagesTests
{
    public class TheBuildAsyncMethod
    {
        [Theory]
        [InlineData(4096,
            """
            user: <@U01CTK59Q07> (Customer) says: First message!
            <@U012LKJFG0P> (Support Agent) says: Second message!
            <@U01CTK59Q07> (Customer) says: Third message!
            <@U012LKJFG0P> (Support Agent) says: Fourth message!

            assistant: This is the current summary. [!conclusion:foo bar baz]

            user: <@U01CTK59Q07> (Customer) says: This should have 20 tokens

            """)]
        [InlineData(37,
            """
            assistant: This is the current summary. [!conclusion:foo bar baz]

            user: <@U01CTK59Q07> (Customer) says: This should have 20 tokens

            """)]
        [InlineData(21, "user: <@U01CTK59Q07> (Customer) says: This should have 20 tokens\n")]
        public async Task BuildsUserAndAssistantChatMessages(int tokensRemaining, string expected)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            conversation.Properties = new ConversationProperties
            {
                Summary = "This is the current summary. [!conclusion:foo bar baz]"
            };
            var customer = new SourceUser("U01CTK59Q07", "Customer");
            var agent = new SourceUser("U012LKJFG0P", "Support Agent");
            var messages = new SourceMessage[]
            {
                new(
                    "First message!",
                    customer,
                    SlackTimestamp.Parse("1681253218.000001"),
                    new CompletionInfo("First Summary", new TokenUsage(2, PromptTokenCount: 6, 8))),
                new(
                    "Second message!",
                    agent,
                    SlackTimestamp.Parse("1681253218.000002"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "Third message!",
                    customer,
                    SlackTimestamp.Parse("1681253218.000003"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "Fourth message!",
                    agent,
                    SlackTimestamp.Parse("1681253218.000004"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new("This should have 20 tokens", customer, SlackTimestamp.Parse("1681253218.000005")),

            };
            Assert.Equal(20, messages.Last().PromptTokenCount);
            var replacements = new Dictionary<string, SecretString>();
            var history = new SanitizedConversationHistory(messages, replacements);

            var prompts = SanitizedPromptMessages.BuildSummarizationPromptMessages(history, conversation, tokensRemaining);

            Assert.Same(replacements, prompts.Replacements);
            Assert.Equal(expected, prompts.Messages.Format());
        }

        [Fact]
        public async Task BuildsMessagesForMultipleNewNotYetSummarizedMessages()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            conversation.Properties = new ConversationProperties
            {
                Summary = "This is the current summary. [!conclusion:foo bar baz]"
            };
            var customer = new SourceUser("U01CTK59Q07", "Customer");
            var agent = new SourceUser("U012LKJFG0P", "Support Agent");
            var messages = new SourceMessage[]
            {
                new(
                    "First message!",
                    customer,
                    SlackTimestamp.Parse("1681253218.000001")),
                new(
                    "Second message!",
                    agent,
                    SlackTimestamp.Parse("1681253218.000002"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "Third message!",
                    customer,
                    SlackTimestamp.Parse("1681253218.000003"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new("Fourth message!", agent, SlackTimestamp.Parse("1681253218.000004")),
                new("This should have 20 tokens", customer, SlackTimestamp.Parse("1681253218.000005")),

            };
            Assert.Equal(20, messages.Last().PromptTokenCount);
            var replacements = new Dictionary<string, SecretString>();
            var history = new SanitizedConversationHistory(messages, replacements);

            var prompts = SanitizedPromptMessages.BuildSummarizationPromptMessages(history, conversation, 4096);

            Assert.Same(replacements, prompts.Replacements);
            const string expected = """
            user: <@U01CTK59Q07> (Customer) says: First message!
            <@U012LKJFG0P> (Support Agent) says: Second message!
            <@U01CTK59Q07> (Customer) says: Third message!

            assistant: This is the current summary. [!conclusion:foo bar baz]

            user: <@U012LKJFG0P> (Support Agent) says: Fourth message!
            <@U01CTK59Q07> (Customer) says: This should have 20 tokens

            """;
            Assert.Equal(expected, prompts.Messages.Format());
        }

        [Fact]
        public async Task BuildsAllMessagesAsSinglePromptWhenAllNotSummarized()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            conversation.Properties = new ConversationProperties
            {
                Summary = "This is the current summary. [!conclusion:foo bar baz]"
            };
            var customer = new SourceUser("U01CTK59Q07", "Customer");
            var agent = new SourceUser("U012LKJFG0P", "Support Agent");
            var messages = new SourceMessage[]
            {
                new("First message!", customer, SlackTimestamp.Parse("1681253218.000001")),
                new("Second message!", agent, SlackTimestamp.Parse("1681253218.000002")),
                new("Third message!", customer, SlackTimestamp.Parse("1681253218.000003")),
                new("Fourth message!", agent, SlackTimestamp.Parse("1681253218.000004")),
                new("This should have 20 tokens", customer, SlackTimestamp.Parse("1681253218.000005")),

            };
            Assert.Equal(20, messages.Last().PromptTokenCount);
            var replacements = new Dictionary<string, SecretString>();
            var history = new SanitizedConversationHistory(messages, replacements);

            var prompts = SanitizedPromptMessages.BuildSummarizationPromptMessages(history, conversation, 4096);

            Assert.Same(replacements, prompts.Replacements);
            const string expected = """
            user: <@U01CTK59Q07> (Customer) says: First message!
            <@U012LKJFG0P> (Support Agent) says: Second message!
            <@U01CTK59Q07> (Customer) says: Third message!
            <@U012LKJFG0P> (Support Agent) says: Fourth message!
            <@U01CTK59Q07> (Customer) says: This should have 20 tokens

            """;
            Assert.Equal(expected, prompts.Messages.Format());
        }

        [Fact]
        public async Task ReturnsEmptyCollectionWhenLastMessagesSummarized()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            conversation.Properties = new ConversationProperties
            {
                Summary = "This is the current summary. [!conclusion:foo bar baz]"
            };
            var customer = new SourceUser("U01CTK59Q07", "Customer");
            var agent = new SourceUser("U012LKJFG0P", "Support Agent");
            var messages = new SourceMessage[]
            {
                new(
                    "First message!",
                    customer,
                    SlackTimestamp.Parse("1681253218.000001")),
                new(
                    "Second message!",
                    agent,
                    SlackTimestamp.Parse("1681253218.000002"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new(
                    "Third message!",
                    customer,
                    SlackTimestamp.Parse("1681253218.000003"),
                    new CompletionInfo("Second Summary", new TokenUsage(2, PromptTokenCount: 5, 7))),
                new("Fourth message!", agent, SlackTimestamp.Parse("1681253218.000004")),
                new(
                    "This should have 20 tokens",
                    customer,
                    SlackTimestamp.Parse("1681253218.000005"),
                    new CompletionInfo("Second Summary",
                        new TokenUsage(2, PromptTokenCount: 5, 7))),

            };
            var replacements = new Dictionary<string, SecretString>();
            var history = new SanitizedConversationHistory(messages, replacements);

            var prompts = SanitizedPromptMessages.BuildSummarizationPromptMessages(history, conversation, 4096);

            Assert.Same(replacements, prompts.Replacements);
            Assert.Empty(prompts.Messages);
        }
    }
}

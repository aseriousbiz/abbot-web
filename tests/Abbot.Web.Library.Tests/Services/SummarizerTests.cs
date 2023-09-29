using Abbot.Common.TestHelpers;
using OpenAI_API.Chat;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Cryptography;
using Serious.Slack;
using Conversation = Serious.Abbot.Entities.Conversation;

public class SummarizerTests
{
    public class TheSummarizeConversationAsyncMethod
    {
        [Fact]
        public async Task SummarizesSingleSlackMessageProtectingSensitiveEmails()
        {
            var env = TestEnvironment.Create<CustomTestData>();
            env.OpenAiClient.PushChatResult("email.1@protected.ab.bot says call me at 111-111-1111.");
            var customer = env.TestData.ForeignMember;
            var messages = new SourceMessage[]
            {
                new(
                    "Text to summarize email.1@protected.ab.bot.",
                    new SourceUser(customer.User.PlatformUserId, "Support member"),
                    SlackTimestamp.Parse("1676486431.793829"))
            };

            var replacements = new Dictionary<string, SecretString>
            {
                ["email.1@protected.ab.bot"] = "me@ab.bot",
                ["111-111-1111"] = "555-121-1234",
            };

            var history = new SanitizedConversationHistory(messages, replacements);
            var summarizer = env.Activate<Summarizer>();

            var result = await summarizer.SummarizeConversationAsync(
                history,
                env.TestData.Conversation,
                customer,
                env.TestData.Organization);

            Assert.NotNull(result);
            var promptMessages = Assert.Single(env.OpenAiClient.ReceivedChatPrompts);
            Assert.Equal(2, promptMessages.Count);
            var systemPrompt = promptMessages[0];
            Assert.Equal(ChatMessageRole.System, systemPrompt.Role);
            var userRolePrompt = promptMessages[1];
            Assert.Equal(
                $"{env.TestData.ForeignMember.ToMention()} (Support member) says: Text to summarize email.1@protected.ab.bot.",
                userRolePrompt.Content);

            Assert.Equal(ChatMessageRole.User, userRolePrompt.Role);
            Assert.Equal("me@ab.bot says call me at 555-121-1234.", result.Summary);
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
    }
}

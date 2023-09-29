using Abbot.Common.TestHelpers;
using Azure.AI.TextAnalytics;
using OpenAI_API.Chat;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

public class ConversationMatcherTests
{
    public class TheIdentifyConversationAsyncMethod
    {
        [Fact]
        public async Task DoesNotReturnsConversationForTopLevelMessageThatIsNotEligibleToStartConversation()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            await env.CreateConversationAsync(room, firstMessageId: "1680118971.744389");
            var message = new ConversationMessage(
                ".test",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow.AddDays(-10),
                MessageId: "1680118971.744390",
                ThreadId: null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);
            var tracker = env.Activate<ConversationMatcher>();

            var result = await tracker.IdentifyConversationAsync(message);

            Assert.Null(result.Result);
            Assert.Null(result.Conversation);
        }

        [Fact]
        public async Task ReturnsConversationFromAIMatch()
        {
            const string ccNumber1 = "2222 4053 4324 8877";
            const string ccNumber2 = "2222 4053 4324 8822";
            const string phoneNumber = "555-123-4567";
            var env = TestEnvironment.Create();
            env.TestData.Organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };
            const string originalConversationLog =
                $""""
                Conversation 3: """
                Participants: <@Uhome> (Support Agent)
                Tags: tag-3
                Last Response: 7 minutes ago
                Summary: My phone number is {phoneNumber}"
                """

                Conversation 2: """
                Participants: <@Uhome> (Support Agent)
                Tags: tag-2, tag-3
                Last Response: 1 hour ago
                Summary: Test Conversation 6"
                """

                Conversation 1: """
                Participants: <@Uhome> (Support Agent)
                Tags: tag-1
                Last Response: 1 hour ago
                Summary: Test Conversation 5"
                """
                """";
            var expectedConversationLog = originalConversationLog.Replace(phoneNumber, "555-100-0001");
            env.TextAnalyticsClient.AddResult(originalConversationLog, phoneNumber, new SensitiveValue[]
            {
                new(phoneNumber, PiiEntityCategory.PhoneNumber, null, 1, originalConversationLog.IndexOf(phoneNumber, StringComparison.Ordinal), phoneNumber.Length)
            });
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            var message = new ConversationMessage(
                "Check benefits for SSN 555-12-1234",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow.AddDays(-10),
                "1680118971.744389",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null)
            {
                SensitiveValues = new[]
                {
                    new SensitiveValue("555-12-1234", PiiEntityCategory.USSocialSecurityNumber, null, 1.0, 23, 11)
                },
                ClassificationResult = new ClassificationResult
                {
                    ReasonedActions = Array.Empty<Reasoned<string>>(),
                    RawCompletion = "null",
                    PromptTemplate = "null",
                    Prompt = "null",
                    Temperature = 0,
                    TokenUsage = new TokenUsage(1, 2, 3),
                    Model = "gpt-4",
                    ProcessingTime = default,
                    Directives = Array.Empty<Directive>(),
                    UtcTimestamp = default
                }
            };
            var tags = (await env.Tags.EnsureTagsAsync(
                    new[] { "tag-1", "tag-2", "tag-3" },
                    null,
                    env.TestData.Abbot,
                    env.TestData.Organization))
                .ToList();
            var firstConversation = await env.CreateConversationAsync(room, firstMessageId: "1680118971.700000");
            await env.Tags.TagConversationAsync(firstConversation, new[] { tags[0].Id }, env.TestData.Abbot.User);
            env.Clock.AdvanceBy(TimeSpan.FromMinutes(7));
            var conversation = await env.CreateConversationAsync(room, firstMessageId: "1680118971.700001");
            var action = new Reasoned<string>("This is part of conversation", $"{conversation.Id}");
            const string historicalMessage = $"My cc number is {ccNumber1} and {ccNumber2}.";
            var conversationMatchResult = CreateMatchResult(
                historicalMessage,
                action,
                action.ToString());
            await env.Conversations.AddTimelineEventAsync(
                conversation,
                env.TestData.ForeignMember,
                env.Clock.UtcNow,
                new MessagePostedEvent
                {
                    MessageId = "1680118971.700002",
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        historicalMessage,
                        sensitiveValues: new[]
                        {
                            new SensitiveValue(ccNumber1, PiiEntityCategory.CreditCardNumber, null, 0.9, 16, ccNumber1.Length),
                            new SensitiveValue(ccNumber2, PiiEntityCategory.CreditCardNumber, null, 0.9, 40, ccNumber2.Length)
                        },
                        conversationMatchAIResult: conversationMatchResult).ToJson(),
                });
            await env.Tags.TagConversationAsync(
                conversation,
                new[] { tags[1].Id, tags[2].Id },
                env.TestData.Abbot.User);
            env.Clock.AdvanceBy(TimeSpan.FromHours(1));
            var thirdConversation = await env.CreateConversationAsync(room, firstMessageId: "1680118971.700002", title: $"My phone number is {phoneNumber}");
            await env.Tags.TagConversationAsync(thirdConversation, new[] { tags[2].Id }, env.TestData.Abbot.User);
            env.Clock.AdvanceBy(TimeSpan.FromMinutes(7));

            // Generate the legacy Reasoned<string> format here because that's what this prompt expects.
            env.OpenAiClient.PushChatResult($"[Thought]It just is[/Thought][Action]{conversation.Id}[/Action]");
            var tracker = env.Activate<ConversationMatcher>();

            var result = await tracker.IdentifyConversationAsync(message);

            Assert.NotNull(result.Result);
            Assert.Equal(conversation.Id, result.Result.CandidateConversationId);
            Assert.NotNull(result.Conversation);
            Assert.Equal(conversation.Id, result.Conversation.Id);
            var chatPromptMessages = Assert.Single(env.OpenAiClient.ReceivedChatPrompts).ToArray();
            var systemPrompt = chatPromptMessages[0].Content;
            var expectedSystemPrompt = AISettingsRegistry.Defaults[AIFeature.ConversationMatcher]
                .Prompt.Text.Replace("{Conversation}", expectedConversationLog);
            Assert.Equal(expectedSystemPrompt, systemPrompt);
            var examplePrompts = chatPromptMessages[1..^1];
            Assert.Equal((ChatMessageRole.User, "<@Uforeign> (Customer) says: My cc number is 1111 1111 1111 1111 and 2222 2222 2222 2222."), (examplePrompts[0].Role, examplePrompts[0].Content));
            Assert.Equal(action.ToString(), examplePrompts[1].Content);
            var userPrompt = chatPromptMessages[^1].Content;
            Assert.Equal("<@Uforeign> (Customer): Check benefits for SSN 999-00-0001", userPrompt);
        }

        [Fact]
        public async Task ReturnsConversationForMessageInConversationThread()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            var existing = await env.CreateConversationAsync(room, firstMessageId: "1111");
            var message = new ConversationMessage(
                "Question?",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow.AddDays(-10),
                "1234",
                "1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);
            var tracker = env.Activate<ConversationMatcher>();

            var result = await tracker.IdentifyConversationAsync(message);

            Assert.NotNull(result.Conversation);
            Assert.Null(result.Result);
            Assert.Equal(existing.Id, result.Conversation.Id);
        }

        [Fact]
        public async Task ReturnsNullConversationWhenConversationDoesNotExist()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            var message = new ConversationMessage(
                "Question?",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow.AddDays(-10),
                "1234",
                "1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);
            var tracker = env.Activate<ConversationMatcher>();

            var result = await tracker.IdentifyConversationAsync(message);

            Assert.Null(result.Conversation);
        }

        [Fact]
        public async Task ReturnsNullConversationFromTopLevelMessageWhenAINotEnabled()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = false
            };
            await env.Db.SaveChangesAsync();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            var message = new ConversationMessage(
                "Question?",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow.AddDays(-10),
                "1680118971.744389",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            await env.CreateConversationAsync(room, firstMessageId: "1680118971.700000");
            var conversation = await env.CreateConversationAsync(room, firstMessageId: "1680118971.700001");
            await env.CreateConversationAsync(room, firstMessageId: "1680118971.700002");

            env.OpenAiClient.PushChatResult(new Reasoned<string>("It just is", $"{conversation.Id}"));
            var tracker = env.Activate<ConversationMatcher>();

            var result = await tracker.IdentifyConversationAsync(message);

            Assert.Null(result.Result);
            Assert.Null(result.Conversation);
        }

        [Fact]
        public async Task ReturnsConversationEvenWhenManagedConversationsNotEnabled()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: false);
            var existing = await env.CreateConversationAsync(room, firstMessageId: "1111");
            var message = new ConversationMessage(
                "Question?",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow.AddDays(-10),
                "1234",
                "1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);
            var tracker = env.Activate<ConversationMatcher>();

            var result = await tracker.IdentifyConversationAsync(message);

            Assert.NotNull(result.Conversation);
            Assert.Null(result.Result);
            Assert.Equal(existing.Id, result.Conversation.Id);
        }
    }

    static ConversationMatchAIResult CreateMatchResult(
        string messagePrompt,
        Reasoned<string> reasonedAction,
        string rawCompletion)
        => new()
        {
            MessagePrompt = messagePrompt,
            CandidateConversationId = 0,
            ReasonedActions = new[] { reasonedAction },
            RawCompletion = rawCompletion,
            PromptTemplate = "null",
            Prompt = new("null"),
            Temperature = 0,
            TokenUsage = new TokenUsage(0, 0, 0),
            Model = "gpt-4",
            ProcessingTime = TimeSpan.Zero,
            Directives = new List<Directive>(),
            UtcTimestamp = DateTime.UtcNow
        };
}

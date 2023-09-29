using Abbot.Common.TestHelpers;
using Azure.AI.TextAnalytics;
using NSubstitute;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Live;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack;

public class AutoSummarizationConsumerTests
{
    const string SlackThreadId = "1675913367.115089";

    public class TheConsumeMethod
    {
        [Fact]
        public async Task UpdatesSummaryAndLastMessagePostedEventWithNewMessagesThreadWhenNewMessageInConversationPublished()
        {
            var env = TestEnvironmentBuilder.Create<CustomTestData>()
                .AddBusConsumer<AutoSummarizationConsumer>()
                .Build();
            var customer = env.TestData.Customer;
            var agent = env.TestData.SupportAgent;
            env.OpenAiClient.PushChatResult("This is a new summary with my phone number 555-100-0001.");
            var organization = env.TestData.Organization;
            var conversation = env.TestData.Conversation;
            conversation.Members.Add(new ConversationMember { Member = agent });
            conversation.Summary = "This is the old summary."; // This gets overwritten.
            conversation.Properties = new()
            {
                Summary = "This is the old summary.", // This gets overwritten.
                Conclusion = "This is the old conclusion.", // This gets overwritten.
            };
            await env.Db.SaveChangesAsync();
            var messagePostedEvents = new MessagePostedEvent[] {
                new() {
                    MessageId = SlackThreadId,
                    Member = customer,
                    MessageUrl = new Uri($"https://example.com/{SlackThreadId}"),
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        "Summarize this phone number (555) 999-1111!",
                        "First summary",
                        sensitiveValues: new SensitiveValue[]
                        {
                            new("(555) 999-1111", PiiEntityCategory.PhoneNumber, null, 1, Offset: 28, Length: 14)
                        }).ToJson(),
                },
                new() {
                    MessageId = "1675913367.115090",
                    Member = agent,
                    MessageUrl = new Uri($"https://example.com/1675913367.115090?thread_ts={SlackThreadId}"),
                    Metadata = TestHelpers.CreateMessagePostedMetadata("Summarize that!", "Second summary").ToJson(),
                },
                new() {
                    MessageId = "1675913367.115091",
                    Member = customer,
                    MessageUrl = new Uri($"https://example.com/1675913367.115091?thread_ts={SlackThreadId}"),
                    Metadata = TestHelpers.CreateMessagePostedMetadata("Summarize those!").ToJson(),
                }
            };
            conversation.Events.AddRange(messagePostedEvents);
            await env.Db.SaveChangesAsync();
            var message = new NewMessageInConversation
            {
                SenderId = customer,
                MessageId = "1675913367.115091",
                ThreadId = SlackThreadId,
                MessageText = "Some text",
                MessageUrl = new Uri("https://example.com/foo/bar"),
                ConversationId = conversation,
                RoomId = conversation.Room,
                OrganizationId = organization,
                IsLive = true,
                ConversationState = ConversationState.Waiting,
                HubId = null,
                HubThreadId = null,
            };

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(conversation);
            var summaryPrompt = Assert.Single(env.OpenAiClient.ReceivedChatPrompts);

            string expected = """
            system: Conversation State: New

            user: <@Uforeign> (Customer) says: Summarize this phone number 555-100-0001!
            <@Uhome> (Support Agent) says: Summarize that!

            assistant: This is the old summary.
            [!conclusion:This is the old conclusion.]

            user: <@Uforeign> (Customer) says: Summarize those!

            """;
            Assert.Equal(expected, summaryPrompt.Format());
            Assert.Equal("This is a new summary with my phone number (555) 999-1111.", conversation.Summary);
            using var _ = env.ActivateInNewScope<ConversationRepository>(out var conversationRepository);
            // Ensure we updated the last message posted event.
            var convo = await conversationRepository.GetConversationAsync(message.ConversationId);
            Assert.NotNull(convo);
            var timeline = await conversationRepository.GetTimelineAsync(convo);
            var lastMessagePostedEvent = timeline
                .OfType<MessagePostedEvent>()
                .SingleOrDefault(m => m.MessageId == "1675913367.115091");
            Assert.NotNull(lastMessagePostedEvent);
            Assert.NotNull(lastMessagePostedEvent.DeserializeMetadata()?.SummarizationResult);

            // Ensure we published the flash message
            env.TestData.AssertFlash();

            // Ensure we re-rendered the hub message
            Assert.True(await env.BusTestHarness.Published.Any<RefreshHubMessage>(r => r.Context.Message.ConversationId == conversation));
        }

        [Fact]
        public async Task DoesNotSummarizeIfEntireThreadIsSummarized()
        {
            var env = TestEnvironmentBuilder.Create<CustomTestData>()
                .Substitute<ISanitizedConversationHistoryBuilder>(out var historyBuilder)
                .AddBusConsumer<AutoSummarizationConsumer>()
                .Build();
            var customer = env.TestData.Customer;
            var agent = env.TestData.SupportAgent;
            env.OpenAiClient.PushChatResult("This is a new summary with my phone number 555-000-0001.");
            var organization = env.TestData.Organization;
            var conversation = env.TestData.Conversation;
            conversation.Members.Add(new ConversationMember { Member = agent });
            conversation.Summary = "This is the old summary."; // This gets overwritten.
            conversation.Properties = new()
            {
                Summary = "This is the old summary.", // This gets overwritten.
                Conclusion = "This is the old conclusion.", // This gets overwritten.
            };
            await env.Db.SaveChangesAsync();
            var replacements = new Dictionary<string, SecretString>
            {
                ["555-000-0001"] = "555-999-1111"
            };

            var messages = new SourceMessage[]
            {
                new(
                    "Summarize this!",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse(SlackThreadId),
                    new CompletionInfo("First summary", new TokenUsage(2, 3, 5))),
                new(
                    "Summarize that!",
                    new SourceUser(agent.User.PlatformUserId, "Agent"),
                    SlackTimestamp.Parse(SlackThreadId),
                    new CompletionInfo("Second summary", new TokenUsage(2, 3, 5))),
                new(
                    "Summarize those!",
                    new SourceUser(customer.User.PlatformUserId, "Customer"),
                    SlackTimestamp.Parse(SlackThreadId),
                    new CompletionInfo("Third summary", new TokenUsage(2, 3, 5))),
            };
            var message = new NewMessageInConversation
            {
                SenderId = customer,
                MessageId = "1675913367.115092",
                ThreadId = SlackThreadId,
                MessageText = "Some text",
                MessageUrl = new Uri("https://example.com/foo/bar"),
                ConversationId = conversation,
                RoomId = conversation.Room,
                OrganizationId = organization,
                IsLive = true,
                ConversationState = ConversationState.Waiting,
                HubId = null,
                HubThreadId = null,
            };
            var sanitizedHistory = new SanitizedConversationHistory(messages, replacements);
            historyBuilder.BuildHistoryAsync(Arg.Is<Conversation>(c => c.Id == conversation.Id))
                .Returns(sanitizedHistory);

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(conversation);
            Assert.Empty(env.OpenAiClient.ReceivedChatPrompts);
            Assert.Equal("This is the old summary.", conversation.Summary);

            // Ensure we published the flash message
            Assert.Empty(env.FlashPublisher.PublishedFlashes);

            // Ensure we re-rendered the hub message
            Assert.False(await env.BusTestHarness.Published.Any<RefreshHubMessage>(r => r.Context.Message.ConversationId == conversation));
        }
    }

    public class CustomTestData : CommonTestData
    {
        TestEnvironmentWithData _env = null!;

        public Conversation Conversation { get; private set; } = null!;

        public Member Customer => ForeignMember;

        public Member SupportAgent => Member;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            _env = env;
            env.TestData.Organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };
            await env.Db.SaveChangesAsync();
            await env.AISettings.SetModelSettingsAsync(
                AIFeature.Summarization,
                new ModelSettings()
                {
                    Model = "x",
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            Conversation State: {{Conversation.State}}
                            """,
                    },
                    Temperature = 1.0,
                },
                env.TestData.Member);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            Conversation = await env.CreateConversationAsync(
                room,
                firstMessageId: SlackThreadId,
                startedBy: env.TestData.ForeignMember);
        }

        public void AssertFlash()
        {
            // Ensure we published the flash message
            var flash = Assert.Single(_env.FlashPublisher.PublishedFlashes);
            Assert.Equal(FlashName.ConversationListUpdated, flash.Name);
            Assert.Equal(FlashGroup.Organization(_env.TestData.Organization), flash.Group);
            Assert.Empty(flash.Arguments);
        }
    }
}

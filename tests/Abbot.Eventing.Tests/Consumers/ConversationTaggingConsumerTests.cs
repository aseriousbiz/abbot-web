using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Live;

public class ConversationTaggingConsumerTests
{
    public class TheConsumeMethod
    {
        [Fact]
        public async Task TagsConversationFromNewMessageInConversationMessage()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<ConversationTaggingConsumer>()
                .Build();

            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            var availableTags = await env.Tags.EnsureTagsAsync(
                new[] { "a-tag" },
                null,
                env.TestData.Member,
                env.TestData.Organization);
            await env.Tags.TagConversationAsync(conversation, new[] { availableTags.Single().Id }, env.TestData.User);
            var message = new NewMessageInConversation
            {
                ConversationId = conversation,
                OrganizationId = organization,
                ClassificationResult = CreateClassificationResult(
                    new Category("sentiment", "negative"),
                    new Category("topic", "social")
                ),
                SenderId = default,
                RoomId = default,
                MessageId = "null",
                ThreadId = null,
                MessageText = "Some text",
                MessageUrl = new Uri("https://example.com/"),
                IsLive = false,
                ConversationState = ConversationState.Unknown,
                HubId = null,
                HubThreadId = null,
            };

            await env.PublishAndWaitForConsumptionAsync(message);

            var reloaded = await env.Conversations.GetConversationAsync(conversation);
            Assert.NotNull(reloaded);
            Assert.Collection(reloaded.Tags.OrderBy(t => t.Tag.Name),
                t => Assert.Equal("a-tag", t.Tag.Name),
                t => Assert.Equal("sentiment:negative", t.Tag.Name),
                t => Assert.Equal("topic:social", t.Tag.Name));

            // Ensure we published the flash message
            var flash = Assert.Single(env.FlashPublisher.PublishedFlashes);
            Assert.Equal(FlashName.ConversationListUpdated, flash.Name);
            Assert.Equal(FlashGroup.Organization(env.TestData.Organization), flash.Group);
            Assert.Empty(flash.Arguments);
        }

        static ClassificationResult CreateClassificationResult(params Category[] categories)
        {
            return new ClassificationResult
            {
                Categories = categories,
                RawCompletion = "null",
                PromptTemplate = "null",
                Prompt = new("null"),
                Temperature = 1,
                TokenUsage = new TokenUsage(0, 0, 0),
                Model = "gpt-4",
                ProcessingTime = TimeSpan.Zero,
                Directives = Array.Empty<Directive>(),
                UtcTimestamp = DateTime.UtcNow,
                ReasonedActions = Array.Empty<Reasoned<string>>(),
            };
        }
    }
}

using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Eventing.Messages;

public class CategorySignalingConsumerTests
{
    const string SlackTimestamp = "1675913367.115089";

    public class TheConsumeMethod
    {
        [Fact]
        public async Task RaisesSignalsForMessageCategories()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<CategorySignalingConsumer>()
                .Build();

            var organization = env.TestData.Organization;
            organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            var customer = env.TestData.ForeignMember;
            var conversation = await env.CreateConversationAsync(
                room,
                firstMessageId: SlackTimestamp,
                startedBy: customer);
            var message = new NewMessageInConversation
            {
                ConversationId = conversation,
                OrganizationId = organization,
                MessageUrl = new Uri("https://example.com/foo/bar"),
                ClassificationResult = CreateClassificationResult(
                    new("sentiment",
                        "negative"),
                    new("topic",
                        "documentation")),
                SenderId = env.TestData.ForeignMember,
                RoomId = room,
                MessageId = SlackTimestamp,
                ThreadId = null,
                MessageText = "Some message",
                IsLive = true,
                ConversationState = ConversationState.New,
                HubId = null,
                HubThreadId = null
            };

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(conversation);
            Assert.Collection(env.SignalHandler.RaisedSignals,
                signal => {
                    Assert.Equal("system:conversation:category:sentiment", signal.Name);
                    Assert.Equal("negative", signal.Arguments);
                    Assert.NotNull(signal.TriggeringMessage);
                    Assert.Equal(SlackTimestamp, signal.TriggeringMessage.MessageId);
                    Assert.Equal(new Uri("https://example.com/foo/bar"), signal.TriggeringMessage.MessageUrl);
                },
                signal => {
                    Assert.Equal("system:conversation:category:topic", signal.Name);
                    Assert.Equal("documentation", signal.Arguments);
                    Assert.NotNull(signal.TriggeringMessage);
                    Assert.Equal(SlackTimestamp, signal.TriggeringMessage.MessageId);
                    Assert.Equal(new Uri("https://example.com/foo/bar"), signal.TriggeringMessage.MessageUrl);
                });
        }
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

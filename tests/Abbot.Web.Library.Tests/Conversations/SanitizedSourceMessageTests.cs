using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.Cryptography;

public class SanitizedSourceMessageTests
{
    public class TheFromMessagePostedEventMethod
    {
        [Fact]
        public async Task CreatesSanitizedSourceMessageFromMessagePostedEventWithoutSummary()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            var messagePostedEvent = new MessagePostedEvent
            {
                MessageId = "1681333607.000001",
                Member = env.TestData.ForeignMember,
                Conversation = conversation,
                Metadata = TestHelpers.CreateMessagePostedMetadata("Hello, world!", "the summary").ToJson()
            };
            var replacements = new Dictionary<string, SecretString>
            {
                ["test"] = "shhh"
            };

            var result = SanitizedSourceMessage.FromMessagePostedEvent(messagePostedEvent, replacements);

            Assert.NotNull(result);
            Assert.Same(replacements, result.Replacements);
            Assert.Equal("<@Uforeign> (Customer) says: Hello, world!", result.SourceMessage.ToPromptText());
            Assert.NotNull(result.SourceMessage.SummaryInfo);
            Assert.Equal("the summary", result.SourceMessage.SummaryInfo.RawCompletion);
        }

        [Fact]
        public async Task ReturnsNullForEmptyText()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            var messagePostedEvent = new MessagePostedEvent
            {
                Member = env.TestData.ForeignMember,
                Conversation = conversation,
                Metadata = new MessagePostedMetadata
                {
                    Categories = Array.Empty<Category>(),
                    Text = null,
                    SensitiveValues = Array.Empty<SensitiveValue>(),
                }.ToJson()
            };

            var result = SanitizedSourceMessage.FromMessagePostedEvent(
                messagePostedEvent,
                new Dictionary<string, SecretString>());

            Assert.Null(result);
        }
    }
}

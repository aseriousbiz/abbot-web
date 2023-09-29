using Abbot.Common.TestHelpers;
using Azure.AI.TextAnalytics;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;

public class SanitizedConversationHistoryBuilderTests
{
    const string SlackTimestamp = "1675913367.115089";

    public class TheBuildHistoryAsyncMethod
    {
        const string Person1 = "Phil Haack";
        const string Person2 = "Paul Nakata";
        const string Ssn1 = "999-99-9999";
        const string Ssn2 = "888-88-8888";

        [Fact]
        public async Task BuildsHistoryFromMessagePostedEvents()
        {
            var env = TestEnvironment.Create<CustomTestData>();
            var (agent, customer) = env.TestData.GetActors();
            env.TestData.Conversation.Events.AddRange(new MessagePostedEvent[]
            {
                new() {
                    Member = customer,
                    MessageId = SlackTimestamp,
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        $"Abbot was founded by {Person1} and {Person2}.",
                        "First summary",
                        sensitiveValues: new SensitiveValue[]
                        {
                            new(Person1, PiiEntityCategory.Person, null, 0.9, 21, Person1.Length),
                            new(Person2, PiiEntityCategory.Person, null, 0.9, 36, Person2.Length)
                        }
                        ).ToJson(),
                },
                new() {
                    Member = agent,
                    MessageId = "1675913367.115090",
                    ThreadId = SlackTimestamp,
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        $"My ssn is {Ssn1} and my partner's is {Ssn2}.",
                        "Second summary",
                        sensitiveValues: new SensitiveValue[]
                        {
                            new(Ssn1, PiiEntityCategory.USSocialSecurityNumber, null, 0.9, 10, Ssn1.Length),
                            new(Ssn2, PiiEntityCategory.USSocialSecurityNumber, null, 0.9, 42, Ssn2.Length)
                        }).ToJson(),
                },
                new() {
                    Member = customer,
                    MessageId = "1675913367.115091",
                    ThreadId = SlackTimestamp,
                    Metadata = TestHelpers.CreateMessagePostedMetadata("Summarize those!", "Third summary").ToJson(),
                },
                new() {
                    Member = agent,
                    MessageId = "1675913367.115092",
                    ThreadId = SlackTimestamp,
                    // No summary because this is the most recent message.
                    Metadata = TestHelpers.CreateMessagePostedMetadata("Summarize these!").ToJson(),
                },
            });
            await env.Db.SaveChangesAsync();
            var builder = env.Activate<SanitizedConversationHistoryBuilder>();

            var history = await builder.BuildHistoryAsync(env.TestData.Conversation);

            Assert.NotNull(history);
            Assert.Equal(4, history.Replacements.Count);
            var expected = """
            <@Uforeign> (Customer) says: Abbot was founded by Person-1 and Person-2.
                - summary: First summary
                - token usage: prompt: 13, completion: 2, total: 15
            <@Uhome> (Support Agent) says: My ssn is 999-00-0003 and my partner's is 999-00-0004.
                - summary: Second summary
                - token usage: prompt: 22, completion: 2, total: 24
            <@Uforeign> (Customer) says: Summarize those!
                - summary: Third summary
                - token usage: prompt: 5, completion: 2, total: 7
            <@Uhome> (Support Agent) says: Summarize these!

            """;
            Assert.Equal(expected, history.Messages.FormatResults());
        }
    }

    public class CustomTestData : CommonTestData
    {
        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            Room = await env.CreateRoomAsync();
            Conversation = await env.CreateConversationAsync(Room);
            await base.SeedAsync(env);
        }

        public Room Room { get; private set; } = null!;

        public Conversation Conversation { get; private set; } = null!;

        public (Member, Member) GetActors() => (Member, ForeignMember);
    }
}

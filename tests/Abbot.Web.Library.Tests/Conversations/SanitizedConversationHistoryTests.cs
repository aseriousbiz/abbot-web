using Abbot.Common.TestHelpers;
using Azure.AI.TextAnalytics;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;

public class SanitizedConversationHistoryTests
{
    public class TheSanitizeMethod
    {
        [Fact]
        public async Task BuildsSanitizedMessageHistory()
        {
            const string person1 = "Phil Haack";
            const string person2 = "Paul Nakata";
            const string ssn1 = "999-99-9999";
            const string ssn2 = "888-88-8888";
            const string phoneNumber1 = "+1 425-121-1234";
            const string phoneNumber2 = "310-866-1001";

            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            var messagePostedEvents = new MessagePostedEvent[]
            {
                new()
                {
                    MessageId = "1681333607.000001",
                    Member = env.TestData.ForeignMember,
                    Conversation = conversation,
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        $"Abbot was founded by {person1} and {person2}.",
                        "Abbot was founded by two people.",
                        sensitiveValues: new[]
                        {
                            new SensitiveValue(person1, PiiEntityCategory.Person, null, 0.9, 21, person1.Length),
                            new SensitiveValue(person2, PiiEntityCategory.Person, null, 0.9, 36, person2.Length)
                        }).ToJson()
                },
                new()
                {
                    MessageId = "1681333607.000002",
                    Member = env.TestData.Member,
                    Conversation = conversation,
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        $"My ssn is {ssn1} and my partner's is {ssn2}.",
                        "Second Summary",
                        sensitiveValues: new[]
                        {
                            new SensitiveValue(ssn1, PiiEntityCategory.USSocialSecurityNumber, null, 0.9, 10, ssn1.Length),
                            new SensitiveValue(ssn2, PiiEntityCategory.USSocialSecurityNumber, null, 0.9, 42, ssn2.Length)
                        }).ToJson()
                },
                new()
                {
                    MessageId = "1681333607.000003",
                    Member = env.TestData.ForeignMember,
                    Conversation = conversation,
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        $"My phone number is {phoneNumber1} and {phoneNumber2}.",
                        sensitiveValues: new[]
                        {
                            new SensitiveValue(phoneNumber1, PiiEntityCategory.PhoneNumber, null, 0.9, 19, phoneNumber1.Length),
                            new SensitiveValue(phoneNumber2, PiiEntityCategory.PhoneNumber, null, 0.9, 39, phoneNumber2.Length)
                        }).ToJson()
                },
                new()
                {
                    MessageId = "1681333607.000004",
                    Member = env.TestData.Member,
                    Conversation = conversation,
                    Metadata = TestHelpers.CreateMessagePostedMetadata(
                        $"My old phone number was {phoneNumber1}!",
                        sensitiveValues: new[]
                        {
                            new SensitiveValue(phoneNumber1, PiiEntityCategory.PhoneNumber, null, 0.9, 24, phoneNumber1.Length),
                        }).ToJson()
                },
            };

            var history = SanitizedConversationHistory.Sanitize(messagePostedEvents);

            Assert.Equal(6, history.Replacements.Count);
            var expectedSanitizedMessages = """
                <@Uforeign> (Customer) says: Abbot was founded by Person-1 and Person-2.
                    - summary: Abbot was founded by two people.
                    - token usage: prompt: 13, completion: 8, total: 21
                <@Uhome> (Support Agent) says: My ssn is 999-00-0003 and my partner's is 999-00-0004.
                    - summary: Second Summary
                    - token usage: prompt: 22, completion: 2, total: 24
                <@Uforeign> (Customer) says: My phone number is 555-100-0005 and 555-100-0006.

                <@Uhome> (Support Agent) says: My old phone number was 555-100-0005!

                """;
            Assert.Equal(expectedSanitizedMessages, history.Messages.FormatResults());
        }
    }
}

using Abbot.Common.TestHelpers;
using OpenAI_API.Chat;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.AI.Responder;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using AIChatMessage = OpenAI_API.Chat.ChatMessage;
using ChatMessage = Serious.Abbot.Eventing.Messages.ChatMessage;

namespace Abbot.Web.Library.Tests.AI.Responder;

public class MagicResponderTests
{
    [UsesVerify]
    public class TheRunTurnAsyncMethod
    {
        static readonly ModelSettings TestModelSettings = new()
        {
            Model = "cool-model",
            Temperature = 1,
            Prompt = new TemplatedPrompt()
            {
                Version = PromptVersion.Version2,
                Text = "Be magical",
            }
        };

        static ChatMessage CreateTestMessage(TestEnvironmentWithData env) => new()
        {
            Event = new ChatEvent
            {
                OrganizationId = env.TestData.Organization,
                SenderId = env.TestData.Member,
                Timestamp = env.Clock.UtcNow,
            },
            Text = "hey bud",
            MessageId = "message-id",
            MentionedUsers = Array.Empty<Id<Member>>(),
        };

        [Fact]
        public async Task MultipleIterationTest()
        {
            var env = TestEnvironment.Create(snapshot: true);
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "That's what I was looking for.",
                new ChatPostCommand { Body = "You did it!" }));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I didn't find what I'm looking for, I'll try different terms.",
                new RemSearchCommand { Terms = new[] { "iter2" } }));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I should search rem",
                new RemSearchCommand { Terms = new[] { "iter1" } }));
            var room = await env.CreateRoomAsync();
            var responder = env.Activate<MagicResponder>();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "iter2",
                Content = "woot",
            },
                env.TestData.User);
            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));
            await responder.RunTurnAsync(session, CreateTestMessage(env));

            // Verify
            await Verify(new {
                Description = "Conducts a simple 3 iteration responder session.",
                Session = session,
                env.SlackApi.PostedMessages,
                env.OpenAiClient.ReceivedChatPrompts,
            });
        }

        [Fact]
        public async Task TooManyIterationsTests()
        {
            var env = TestEnvironment.Create(snapshot: true);
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "That's what I was looking for.",
                new ChatPostCommand { Body = "You did it!" }));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I didn't find what I'm looking for, I'll try different terms.",
                new RemSearchCommand { Terms = new[] { "iter4" } }));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I didn't find what I'm looking for, I'll try different terms.",
                new RemSearchCommand { Terms = new[] { "iter3" } }));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I didn't find what I'm looking for, I'll try different terms.",
                new RemSearchCommand { Terms = new[] { "iter2" } }));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I should search rem",
                new RemSearchCommand { Terms = new[] { "iter1" } }));
            var room = await env.CreateRoomAsync();
            var responder = env.Activate<MagicResponder>();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "iter4", // It'll never get here
                Content = "woot",
            },
                env.TestData.User);
            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => responder.RunTurnAsync(session, CreateTestMessage(env)));
            Assert.Equal("Exceeded maximum iteration count without ending the turn.", ex.Message);

            // Verify
            await Verify(new {
                Description = "Fails because the maximum number of iterations is reached. The turn is not saved to the session.",
                Session = session,
                env.SlackApi.PostedMessages,
                env.OpenAiClient.ReceivedChatPrompts,
            });
        }

        [Fact]
        public async Task OpenAIErrorMidTurn()
        {
            var env = TestEnvironment.Create(snapshot: true);
            env.Clock.Freeze();
            env.OpenAiClient.PushChatResult(new Exception("AI became sentient and had to be shut down"));
            env.OpenAiClient.PushChatResult(new Reasoned<Command>(
                "I should search rem",
                new RemSearchCommand { Terms = new[] { "iter1" } }));
            var room = await env.CreateRoomAsync();
            var responder = env.Activate<MagicResponder>();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "iter4", // It'll never get here
                Content = "woot",
            },
                env.TestData.User);
            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            await responder.RunTurnAsync(session, CreateTestMessage(env));

            // Verify
            await Verify(new {
                Description = "Fails because an error is thrown in the second iteration. Turn is not saved to the session. Error is logged",
                Session = session,
                env.SlackApi.PostedMessages,
                env.OpenAiClient.ReceivedChatPrompts,
                Logs = env.GetAllLogs<MagicResponder>(),
            });
        }
    }
}

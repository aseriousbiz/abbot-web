using Abbot.Common.TestHelpers;
using OpenAI_API.Chat;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.AI.Responder;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using AIChatMessage = OpenAI_API.Chat.ChatMessage;

namespace Abbot.Web.Library.Tests.AI.Commands;

public static class CommandExecutorTests
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

    public class TheNoopCommand
    {
        [Fact]
        public async Task EndsTurnWithNoMessage()
        {
            var env = TestEnvironment.Create();
            var command = new NoopCommand();
            var room = await env.CreateRoomAsync();

            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var executor = env.Activate<CommandExecutor>();
            var result = await executor.ExecuteCommandAsync(session, command);

            var endTurnResult = Assert.IsType<EndTurnResult>(result);
            Assert.Null(endTurnResult.ResponseMessage);
        }
    }

    public class TheChatPostCommand
    {
        [Fact]
        public async Task EndsTurnWithMessage()
        {
            var env = TestEnvironment.Create();
            var command = new ChatPostCommand { Body = "Hello world" };
            var room = await env.CreateRoomAsync();

            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var executor = env.Activate<CommandExecutor>();
            var result = await executor.ExecuteCommandAsync(session, command);

            var endTurnResult = Assert.IsType<EndTurnResult>(result);
            Assert.Equal("Hello world", endTurnResult.ResponseMessage);
        }
    }

    public class TheRemSearchCommand
    {
        [Fact]
        public async Task NoResults()
        {
            var env = TestEnvironment.Create();
            var command = new RemSearchCommand() { Terms = new[] { "is-a-match" } };
            var room = await env.CreateRoomAsync();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "no-match",
                Content = "This is not the memory you are looking for."
            }, env.TestData.User);

            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var executor = env.Activate<CommandExecutor>();
            var result = await executor.ExecuteCommandAsync(session, command);

            var continueTurnResult = Assert.IsType<ContinueTurnResult>(result);
            Assert.Equal(
                "There are NO memories matching ANY of the provided terms.",
                continueTurnResult.NextRequest);
        }

        [Fact]
        public async Task HasResults()
        {
            var env = TestEnvironment.Create();
            var command = new RemSearchCommand() { Terms = new[] { "is", "a", "match" } };
            var room = await env.CreateRoomAsync();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "match-1",
                Content = "It's a match!",
            }, env.TestData.User);
            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "match-2",
                Content = "It's also a match!",
            }, env.TestData.User);
            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "nope",
                Content = "Not a match",
            }, env.TestData.User);

            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var executor = env.Activate<CommandExecutor>();
            var result = await executor.ExecuteCommandAsync(session, command);

            var continueTurnResult = Assert.IsType<ContinueTurnResult>(result);
            Assert.Equal(
                """
                I found the following memories:

                match-1 = It's a match!
                match-2 = It's also a match!
                """,
                continueTurnResult.NextRequest);
        }
    }

    public class TheRemGetCommand
    {
        [Fact]
        public async Task NoResults()
        {
            var env = TestEnvironment.Create();
            var command = new RemGetCommand() { Key = "match" };
            var room = await env.CreateRoomAsync();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "no-match",
                Content = "This is not the memory you are looking for."
            }, env.TestData.User);

            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var executor = env.Activate<CommandExecutor>();
            var result = await executor.ExecuteCommandAsync(session, command);

            var continueTurnResult = Assert.IsType<ContinueTurnResult>(result);
            Assert.Equal(
                "I don't know what `match` is.",
                continueTurnResult.NextRequest);
        }

        [Fact]
        public async Task HasResults()
        {
            var env = TestEnvironment.Create();
            var command = new RemGetCommand() { Key = "match" };
            var room = await env.CreateRoomAsync();

            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "match",
                Content = "It's a match!",
            }, env.TestData.User);
            await env.Memories.CreateAsync(new Memory()
            {
                Organization = env.TestData.Organization,
                Name = "nope",
                Content = "Not a match",
            }, env.TestData.User);

            var session = new ResponderSession(
                "test",
                env.TestData.Organization,
                room,
                "thread-id",
                env.TestData.Member,
                TestModelSettings,
                new(ChatMessageRole.System, TestModelSettings.Prompt.Text));

            var executor = env.Activate<CommandExecutor>();
            var result = await executor.ExecuteCommandAsync(session, command);

            var continueTurnResult = Assert.IsType<ContinueTurnResult>(result);
            Assert.Equal(
                """
                `match` is `It's a match!`.
                """,
                continueTurnResult.NextRequest);
        }
    }
}

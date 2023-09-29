using Abbot.Common.TestHelpers;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;
using Serious.TestHelpers;

public class ProactiveMessengerTests
{
    public class TestData : CommonTestData
    {
        public Skill Skill { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            Skill = await env.CreateSkillAsync("test-skill");
        }
    }

    [Obsolete("Use other overload")]
    public class TheSendMessageWithProactiveBotMessageMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("123421343.342322")]
        public async Task DispatchesToAddressInMessageOptions(string? threadId)
        {
            var messageDispatcher = new FakeMessageDispatcherWrapper();
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IMessageDispatcher>(messageDispatcher)
                .Build();
            var organization = env.TestData.Organization;
            var chatAddress = new ChatAddress(ChatAddressType.Room, "C00000123", threadId);
            var message = new ProactiveBotMessage
            {
                Message = "Hello, world!",
                SkillId = env.TestData.Skill.Id,
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress(ChatAddressType.Room, "C00000123", threadId)
                }
            };
            var messenger = env.Activate<ProactiveMessenger>();

            await messenger.SendMessageAsync(message);

            Assert.NotNull(messageDispatcher.DispatchedMessage);
            Assert.Equal(message.Message, messageDispatcher.DispatchedMessage.Message.Text);
            Assert.Equal(chatAddress, messageDispatcher.DispatchedMessage.Message.To);
            Assert.Equal(organization.Id, messageDispatcher.DispatchedMessage.Organization.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("123421343.342322")]
        public async Task DispatchesToAddressInLegacyConversationReference(string? threadId)
        {
            var messageDispatcher = new FakeMessageDispatcherWrapper();
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IMessageDispatcher>(messageDispatcher)
                .Build();
            var organization = env.TestData.Organization;
            var message = new ProactiveBotMessage
            {
                Message = "Happy trails!",
                SkillId = env.TestData.Skill.Id,
                ConversationReference = new ConversationReference
                {
                    Conversation = new ConversationAccount
                    {
                        Id = new SlackConversationId("C00000123", threadId).ToString(),
                        ConversationType = "channel"
                    },
                }
            };
            var messenger = env.Activate<ProactiveMessenger>();

            await messenger.SendMessageAsync(message);

            Assert.NotNull(messageDispatcher.DispatchedMessage);

            Assert.Equal(message.Message, messageDispatcher.DispatchedMessage.Message.Text);
            Assert.Equal("C00000123", messageDispatcher.DispatchedMessage.Message.To.Id);
            Assert.Equal(threadId, messageDispatcher.DispatchedMessage.Message.To.ThreadId);
            Assert.Equal(organization.Id, messageDispatcher.DispatchedMessage.Organization.Id);
        }

        [Fact]
        public async Task DoesNotDispatchWhenSkillNotFound()
        {
            var messageDispatcher = new FakeMessageDispatcherWrapper();
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IMessageDispatcher>(messageDispatcher)
                .Build();
            var messenger = env.Activate<ProactiveMessenger>();

            await messenger.SendMessageAsync(new ProactiveBotMessage
            {
                Message = "Unhappy trails!",
                Options = new ProactiveBotMessageOptions
                {
                    To = new ChatAddress(ChatAddressType.Room, "C00000123")
                },
                SkillId = 42
            });

            Assert.Null(messageDispatcher.DispatchedMessage);
        }
    }

    public class TheSendMessageMethod
    {
        [Theory]
        [InlineData(true, null)]
        [InlineData(true, "123421343.342322")]
        [InlineData(false, null)]
        [InlineData(false, "123421343.342322")]
        public async Task DispatchesMessage(bool success, string? threadId)
        {
            var messageDispatcher = new FakeMessageDispatcherWrapper();
            messageDispatcher.Success = success;

            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IMessageDispatcher>(messageDispatcher)
                .Build();
            var organization = env.TestData.Organization;
            var chatAddress = new ChatAddress(ChatAddressType.Room, "C00000123", threadId);
            var message = new BotMessageRequest(
                "Hello, world!",
                chatAddress);
            var messenger = env.Activate<ProactiveMessenger>();

            var response = await messenger.SendMessageAsync(env.TestData.Skill, message);

            Assert.Equal(success, response?.Success);

            Assert.NotNull(messageDispatcher.DispatchedMessage);
            Assert.Equal(message.Text, messageDispatcher.DispatchedMessage.Message.Text);
            Assert.Equal(chatAddress, messageDispatcher.DispatchedMessage.Message.To);
            Assert.Equal(organization.Id, messageDispatcher.DispatchedMessage.Organization.Id);
        }

        [Fact]
        public async Task DoesNotDispatchWhenSkillNotFound()
        {
            var messageDispatcher = new FakeMessageDispatcherWrapper();
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IMessageDispatcher>(messageDispatcher)
                .Build();
            var messenger = env.Activate<ProactiveMessenger>();

            await messenger.SendMessageAsync(new(42), new BotMessageRequest(
                "Happy trails!",
                new ChatAddress(ChatAddressType.Room, "C00000123")
            ));

            Assert.Null(messageDispatcher.DispatchedMessage);
        }
    }

    public class TheSendMessageFromSkillMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("123421343.342322")]
        public async Task DispatchesMessage(string? threadId)
        {
            var messageDispatcher = new FakeMessageDispatcherWrapper();
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IMessageDispatcher>(messageDispatcher)
                .Build();
            var organization = env.TestData.Organization;
            var chatAddress = new ChatAddress(ChatAddressType.Room, "C00000123", threadId);
            var message = new BotMessageRequest(
                "Hello, world!",
                chatAddress);
            var messenger = env.Activate<ProactiveMessenger>();

            await messenger.SendMessageFromSkillAsync(env.TestData.Skill, message);

            Assert.NotNull(messageDispatcher.DispatchedMessage);
            Assert.Equal(message.Text, messageDispatcher.DispatchedMessage.Message.Text);
            Assert.Equal(chatAddress, messageDispatcher.DispatchedMessage.Message.To);
            Assert.Equal(organization.Id, messageDispatcher.DispatchedMessage.Organization.Id);
        }
    }
}

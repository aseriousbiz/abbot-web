using NSubstitute;
using Serious.Abbot.Functions;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class AbbotBotTests
{
    public class TheToStringMethod
    {
        [Fact]
        public void FormatsBotMentionForPlatform()
        {
            var skillInfo = new SkillInfo
            {
                Bot = new PlatformUser("U123", "abbot", "abbot"),
                From = new PlatformUser(),
            };
            var message = new SkillMessage { RunnerInfo = new SkillRunnerInfo(), SkillInfo = skillInfo };
            var skillContextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "ApiKey")
            };
            var brain = new FakeBotBrain();
            var secrets = new FakeSecrets();
            var bot = new AbbotBot(
                skillContextAccessor,
                brain,
                secrets,
                new FakeTickets(),
                new FakeBotHttpClient(),
                new PassiveBotReplyClient(),
                new FakeBotUtilities(),
                new FakeRoomsClient(),
                new FakeUsersClient(),
                new FakeSignaler(),
                new FakeSlackClient(),
                null!,
                null!,
                null!);

            var botMention = bot.ToString();

            Assert.Equal("<@U123>", botMention);
        }
    }

    public class TheProperties
    {
        [Fact]
        public void RoomIsDerivedFromRoomIdAndName()
        {
            var skillInfo = new SkillInfo
            {
                Bot = new PlatformUser("U123", "abbot", "abbot"),
                From = new PlatformUser(),
                Room = new PlatformRoom("7", "Midgar"),
            };
            var bot = CreateBot(skillInfo);

            Assert.Equal("7", bot.Room.Id);
            Assert.Equal("Midgar", bot.Room.Name);
            Assert.Equal(new ChatAddress(ChatAddressType.Room, "7"), bot.Room.Address);
        }

        [Fact]
        public void FromIsDerivedFromSkillFrom()
        {
            var skillInfo = new SkillInfo
            {
                Bot = new PlatformUser("U123", "abbot", "abbot"),
                From = new PlatformUser("U456", "cloud", "Cloud Strife"),
            };
            var bot = CreateBot(skillInfo);

            Assert.Equal("U456", bot.From.Id);
            Assert.Equal("cloud", bot.From.UserName);
            Assert.Equal("Cloud Strife", bot.From.Name);
            Assert.Equal(new ChatAddress(ChatAddressType.User, "U456"), bot.From.Address);
        }

        [Fact]
        public void MessageIdIsDerivedFromSkillInfo()
        {
            var skillInfo = new SkillInfo
            {
                Bot = new PlatformUser("U123", "abbot", "abbot"),
                From = new PlatformUser("U456", "cloud", "Cloud Strife"),
                Message = new SourceMessageInfo
                {
                    MessageId = "42skidoo",
                    Text = "some text",
                    ThreadId = null,
                    MessageUrl = new Uri("https://example.com/foo/bar"),
                    Author = new PlatformUser("U456", "cloud", "Cloud Strife"),
                },
            };
            var bot = CreateBot(skillInfo);

            Assert.Equal("42skidoo", bot.MessageId);
        }

        [Fact]
        public void ThreadIsDerivedFromMessageId()
        {
            var skillInfo = new SkillInfo
            {
                Bot = new PlatformUser("U123", "abbot", "abbot"),
                From = new PlatformUser("U456", "cloud", "Cloud Strife"),
                Message = new SourceMessageInfo
                {
                    MessageId = "42skidoo",
                    Text = "some text",
                    ThreadId = null,
                    MessageUrl = new Uri("https://example.com/foo/bar"),
                    Author = new PlatformUser("U456", "cloud", "Cloud Strife"),
                },
                Room = new PlatformRoom("C123", "Midgar"),
            };
            var bot = CreateBot(skillInfo);

            Assert.NotNull(bot.Thread);
            Assert.Equal(new ChatAddress(ChatAddressType.Room, "C123", "42skidoo"), bot.Thread.Address);
        }

        static AbbotBot CreateBot(SkillInfo skillInfo)
        {
            var message = new SkillMessage { RunnerInfo = new SkillRunnerInfo(), SkillInfo = skillInfo };
            var skillContextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "ApiKey")
            };
            var bot = new AbbotBot(
                skillContextAccessor,
                new FakeBotBrain(),
                new FakeSecrets(),
                new FakeTickets(),
                new FakeBotHttpClient(),
                new PassiveBotReplyClient(),
                new FakeBotUtilities(),
                new FakeRoomsClient(),
                new FakeUsersClient(),
                new FakeSignaler(),
                new FakeSlackClient(),
                null!,
                null!,
                null!);
            return bot;
        }
    }

    public class TheReplyMethods
    {
        const string TestFromUserId = "U456";

        [Fact]
        public async Task CanSendPublicReply()
        {
            var (replyClient, bot) = CreateTestBot();

            await bot.ReplyAsync("Hello");

            Assert.Collection(replyClient.SentReplies,
                r => {
                    Assert.Equal("Hello", r.Message);
                });
        }

        [Fact]
        public async Task CanSendDMToSender()
        {
            var (replyClient, bot) = CreateTestBot();

            await bot.ReplyAsync("Hello", directMessage: true);

            Assert.Collection(replyClient.SentReplies,
                r => {
                    Assert.Equal("Hello", r.Message);
                    Assert.Equal(new ChatAddress(ChatAddressType.User, TestFromUserId), r.Options?.To?.Address);
                });
        }

        [Fact]
        public async Task CanDMOtherUser()
        {
            var (replyClient, bot) = CreateTestBot();

            var other = new PlatformUser("U789", "aerith", "Aerith Gainsborough");

            await bot.ReplyAsync("Hello", new MessageOptions() { To = other });

            Assert.Collection(replyClient.SentReplies,
                r => {
                    Assert.Equal("Hello", r.Message);
                    Assert.Equal(new ChatAddress(ChatAddressType.User, other.Id), r.Options?.To?.Address);
                });
        }

        [Fact]
        public async Task CanDMOtherUserLater()
        {
            var (replyClient, bot) = CreateTestBot();

            var other = new PlatformUser("U789", "aerith", "Aerith Gainsborough");

            await bot.ReplyLaterAsync("Hello", TimeSpan.FromSeconds(5), new MessageOptions() { To = other });

            Assert.Collection(replyClient.SentReplies,
                r => {
                    Assert.Equal("Hello", r.Message);
                    Assert.Equal(new ChatAddress(ChatAddressType.User, other.Id), r.Options?.To?.Address);
                    Assert.Equal(TimeSpan.FromSeconds(5), r.Delay);
                });
        }

        [Fact]
        public async Task CanSendButtonsToOtherUser()
        {
            var (replyClient, bot) = CreateTestBot();

            var other = new PlatformUser("U789", "cloud", "Cloud Strife");

            await bot.ReplyWithButtonsAsync(
                "Excuse me. What happened?",
                new[] { new Button("Nothing...hey, listen."), new Button("You'd better get out of here.") },
                "Choice",
                new Uri("http://example.com"),
                "Title",
                color: "#E3CF8E",
                new MessageOptions() { To = other });

            Assert.Collection(replyClient.SentReplies,
                r => {
                    Assert.Equal("Excuse me. What happened?", r.Message);
                    Assert.Equal(new ChatAddress(ChatAddressType.User, other.Id), r.Options?.To?.Address);
                    Assert.Equal(new[] { "Nothing...hey, listen.", "You'd better get out of here." },
                        r.Buttons.Select(b => b.Title).ToArray());
                    Assert.Equal("Choice", r.ButtonsLabel);
                    Assert.Equal("http://example.com/", r.Image);
                    Assert.Equal("Title", r.Title);
                    Assert.Null(r.TitleUrl);
                    Assert.Equal("#E3CF8E", r.Color);
                });
        }

        [Fact]
        public async Task CanSendImageToOtherUser()
        {
            var (replyClient, bot) = CreateTestBot();

            var other = new PlatformUser("U789", "cloud", "Cloud Strife");

            await bot.ReplyWithImageAsync(
                "https://user-images.githubusercontent.com/7574/145275699-479e4a0d-6a6d-4ab6-8e07-268234d3a019.png",
                "Here, take this.",
                "Flowers",
                new Uri("https://example.com"),
                "#E3CF8E",
                new MessageOptions() { To = other });

            Assert.Collection(replyClient.SentReplies,
                r => {
                    Assert.Equal("Here, take this.", r.Message);
                    Assert.Equal(new ChatAddress(ChatAddressType.User, other.Id), r.Options?.To?.Address);
                    Assert.NotNull(r.TitleUrl);
                    Assert.Equal("https://example.com/", r.TitleUrl.ToString());
                    Assert.Equal("Flowers", r.Title);
                    Assert.Equal("https://user-images.githubusercontent.com/7574/145275699-479e4a0d-6a6d-4ab6-8e07-268234d3a019.png", r.Image);
                    Assert.Equal("#E3CF8E", r.Color);
                });
        }

        [Fact]
        public async Task CanSendTableToOtherUser()
        {
            var (replyClient, bot) = CreateTestBot();

            var other = new PlatformUser("U789", "cloud", "Cloud Strife");

            await bot.ReplyTableAsync(
                new[]
                {
                    new { Name = "Biggs", HP = 100, MP = 20 },
                    new { Name = "Wedge", HP = 150, MP = 30 },
                    new { Name = "Jessie", HP = 120, MP = 50 },
                },
                new MessageOptions() { To = other });

            Assert.Collection(replyClient.SentReplies,
                r => {
                    const string expected = @"```
     Name   |  HP | MP
     ------ | ---:| --:
     Biggs  | 100 | 20
     Wedge  | 150 | 30
     Jessie | 120 | 50
```";
                    Assert.NotNull(r.Message);
                    Assert.Equal(expected.ReplaceLineEndings(), r.Message.ReplaceLineEndings());
                    Assert.Equal(new ChatAddress(ChatAddressType.User, other.Id), r.Options?.To?.Address);
                });
        }

        static (FakeBotReplyClient replyClient, AbbotBot bot) CreateTestBot()
        {
            var skillInfo = new SkillInfo
            {
                Bot = new PlatformUser("U123", "abbot", "abbot"),
                From = new PlatformUser(TestFromUserId, "aerith", "Aerith Gainsborough"),
            };
            var message = new SkillMessage { RunnerInfo = new SkillRunnerInfo(), SkillInfo = skillInfo };
            var skillContextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "ApiKey")
            };
            var replyClient = new FakeBotReplyClient();
            var bot = new AbbotBot(
                skillContextAccessor,
                new FakeBotBrain(),
                new FakeSecrets(),
                new FakeTickets(),
                new FakeBotHttpClient(),
                replyClient,
                new FakeBotUtilities(),
                new FakeRoomsClient(),
                new FakeUsersClient(),
                new FakeSignaler(),
                new FakeSlackClient(),
                null!,
                null!,
                null!);
            return (replyClient, bot);
        }
    }

    public class TheRepliesProperty
    {
        [Fact]
        public async Task RetrievesRepliesFromBotReplyClient()
        {
            var environment = new FakeEnvironment
            {
                { "SkillApiBaseUriFormatString", "https://example.com/skills/{0}" }
            };
            var skillMessage = new SkillMessage
            {
                RunnerInfo = new SkillRunnerInfo
                {
                    SkillId = 42,
                },
                SkillInfo = new SkillInfo
                {
                    SkillName = "test-skill",
                },
                PassiveReplies = true,
            };
            var skillContext = new SkillContext(skillMessage, "apiKey");
            var skillContextAccessor = Substitute.For<ISkillContextAccessor>();
            skillContextAccessor.SkillContext.Returns(skillContext);
            var activeBotReplyClient = new ActiveBotReplyClient(
                new FakeSkillApiClient(42),
                environment,
                skillContextAccessor);
            var botReplyClient = new BotReplyClient(activeBotReplyClient);
            await botReplyClient.SendReplyAsync("reply", TimeSpan.Zero, options: null);
            var brain = new FakeBotBrain();
            var secrets = new FakeSecrets();
            var bot = new AbbotBot(
                skillContextAccessor,
                brain,
                secrets,
                new FakeTickets(),
                new FakeBotHttpClient(),
                botReplyClient,
                new FakeBotUtilities(),
                new FakeRoomsClient(),
                new FakeUsersClient(),
                new FakeSignaler(),
                new FakeSlackClient(),
                null!,
                null!,
                null!);

            var didReply = bot.DidReply;

            Assert.True(didReply);
            var reply = Assert.Single(bot.Replies);
            Assert.Equal("reply", reply);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.TestHelpers;
using Xunit;

public class IntegrationTests
{
    [Theory]
    [InlineData(PlatformType.Slack, "<@U013WCHH9NU>", "U013WCHH9NU", "B0136HA6VJ6")]
    public async Task CallsRemoteSkillWithAlias(
        PlatformType platformType,
        string botMention,
        string botUserId,
        string botId)
    {
        var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId, botId);
        var skill = new Skill
        {
            Name = "pug",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);
        await adapter.SendTextToBotAndGetNextReplyAsync($"{botMention} alias add dog pug bomb");

        var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
            $"{botMention} dog");

        Assert.NotNull(reply);
        Assert.Equal("Got your message loud and clear!", reply.GetReplyMessageText());
    }

    [Fact]
    public async Task CallsRemoteSkillWithPattern()
    {
        var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack, "U0123456", "B012345");
        var skill = new Skill
        {
            Name = "yelling",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization,
            Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "yelling-pattern",
                    Slug = "yelling-pattern",
                    CaseSensitive = true,
                    PatternType = PatternType.RegularExpression,
                    Pattern = "(?:[A-Z]+\\s*)+[\\?\\!]*"
                }
            }
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);
        var reply = await adapter.SendTextToBotAndGetNextReplyAsync("WHY ARE WE YELLING?!");

        Assert.NotNull(reply);
        Assert.Equal("Got your message loud and clear!", reply.GetReplyMessageText());
    }

    [Fact]
    public async Task DoesNotCallSkillWithDisabledPattern()
    {
        var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack, "U0123456", "B012345");
        var skill = new Skill
        {
            Name = "yelling",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization,
            Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "yelling-pattern",
                    Slug = "yelling-slug",
                    CaseSensitive = true,
                    PatternType = PatternType.RegularExpression,
                    Pattern = "(?:[A-Z]+\\s*)+[\\?\\!]*",
                    Enabled = false,
                }
            }
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);
        var reply = await adapter.SendTextToBotAndGetNextReplyAsync("WHY ARE WE YELLING?!");

        Assert.Null(reply);
    }

    [Fact]
    public async Task CallsMultipleSkillsWithPattern()
    {
        var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack, "U0123456", "B012345");
        var skill = new Skill
        {
            Name = "yelling",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization,
            Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "match-why",
                    Slug = "match-why",
                    CaseSensitive = true,
                    PatternType = PatternType.StartsWith,
                    Pattern = "WHY",
                    Created = DateTime.UtcNow
                },
                new()
                {
                    Name = "yelling-pattern",
                    Slug = "yelling-pattern",
                    CaseSensitive = true,
                    PatternType = PatternType.RegularExpression,
                    Pattern = "(?:[A-Z]+\\s*)+[\\?\\!]*",
                    Created = DateTime.UtcNow.AddDays(-1)
                }
            }
        };
        var anotherSkill = new Skill
        {
            Name = "well-actually",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization,
            Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "catch-all",
                    Slug = "catch-all",
                    CaseSensitive = true,
                    PatternType = PatternType.RegularExpression,
                    Pattern = ".*",
                    Created = DateTime.UtcNow.AddDays(-1)
                }
            }
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        adapter.PushSkillRunResponse("Keep asking WHY.");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);
        await skillRepository.CreateAsync(anotherSkill, user);
        var reply = await adapter.SendTextToBotAndGetNextReplyAsync("WHY ARE WE YELLING?!");
        Assert.NotNull(reply);
        var nextReply = await adapter.GetNextReplyAsync();
        Assert.NotNull(nextReply);
        var reply2 = Assert.IsAssignableFrom<IMessageActivity>(nextReply);

        Assert.Equal("Keep asking WHY.", reply.GetReplyMessageText());
        Assert.Equal("Got your message loud and clear!", reply2.GetReplyMessageText());
    }

    [Theory]
    [InlineData(PlatformType.Slack, "<@U013WCHH9NU>", "U013WCHH9NU")]
    [InlineData(PlatformType.Slack, "howdy <@U013WCHH9NU> ", "U013WCHH9NU")]
    public async Task AllowsCallingAnotherSkillWithAlias(
        PlatformType platformType,
        string botMention,
        string botUserId)
    {
        var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);
        await adapter.SendTextToBotAndGetNextReplyAsync($"{botMention} alias add bark echo bow wow");

        var reply = await adapter.SendTextToBotAndGetNextReplyAsync($"{botMention} bark ruff ruff");

        Assert.NotNull(reply);
        Assert.Equal("bow wow ruff ruff", reply.GetReplyMessageText());
    }

    [Theory]
    [InlineData(PlatformType.Slack, "<@U013WCHH9NU>", "U013WCHH9NU")]
    public async Task CallsRemoteSkillWithNoArguments(
        PlatformType platformType,
        string botMention,
        string botUserId)
    {
        var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);
        var skill = new Skill
        {
            Name = "pug",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);

        var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
            $"{botMention} pug");

        Assert.NotNull(reply);
        Assert.Equal("Got your message loud and clear!", reply.GetReplyMessageText());
    }

    [Fact]
    public async Task RepliesWithHelpfulMessageWhenDirectMessage()
    {
        var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack);
        adapter.Conversation.Conversation.IsGroup = false;
        adapter.Conversation.Conversation.ConversationType = "personal";
        var skill = new Skill
        {
            Name = "pug",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);

        var reply = await adapter.SendTextToBotAndGetNextReplyAsync("pug");

        var richReply = Assert.IsType<RichActivity>(reply);
        var channelData = Assert.IsType<MessageChannelData>(richReply.ChannelData);
        Assert.Equal(":wave: Hey! Thanks for the message. What would you like to do next?", channelData.Message.Text);
        var section = Assert.IsType<Section>(richReply.Blocks[0]);
        Assert.Equal(channelData.Message.Text, section.Text?.Text);
    }

    [Fact]
    public async Task CallsSkillAsDirectMessageWhenMentioned()
    {
        var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack, "U013WCHH9NU");
        adapter.Conversation.Conversation.IsGroup = false;
        adapter.Conversation.Conversation.ConversationType = "personal";

        var skill = new Skill
        {
            Name = "pug",
            Language = CodeLanguage.CSharp,
            Organization = adapter.Organization
        };
        adapter.PushSkillRunResponse("Got your message loud and clear!");
        var (user, _) = await adapter.CreateUser();
        var skillRepository = adapter.GetService<ISkillRepository>()!;
        await skillRepository.CreateAsync(skill, user);

        var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
            "<@U013WCHH9NU> pug");

        Assert.NotNull(reply);
        Assert.Equal("Got your message loud and clear!", reply.GetReplyMessageText());
    }

    public class TheRememberSkill
    {
        [Theory]
        [InlineData(PlatformType.Slack, "<@U12345679>", "U12345679", "paul's address", "paul's address")]
        [InlineData(PlatformType.Slack, "<@U12345679>", "U12345679", "paul’s address", "paul’s address")]
        public async Task NormalizesTypographicApostropheWhenStoringAndRetrievingValue(
            PlatformType platformType,
            string botMention,
            string botUserId,
            string storeArgs,
            string retrieveArgs)
        {
            var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
                $"{botMention} rem {storeArgs} is UNDISCLOSED LOCATION");

            Assert.NotNull(reply);
            Assert.Equal(@"Ok! I will remember that `paul's address` is `UNDISCLOSED LOCATION`.",
                reply.GetReplyMessageText());

            var retrieveReply = await adapter.SendTextToBotAndGetNextReplyAsync(
                $"{botMention} rem {retrieveArgs}");

            Assert.NotNull(retrieveReply);
            Assert.Equal("UNDISCLOSED LOCATION", retrieveReply.GetReplyMessageText());
        }

        [Theory]
        [InlineData(PlatformType.Slack, "<@U12345679>", "U12345679")]
        public async Task RespondsWhenNoSkillAttempted(
            PlatformType platformType,
            string botMention,
            string botUserId)
        {
            var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);
            var skill = new Skill
            {
                Name = "pug",
                Language = CodeLanguage.CSharp,
                Organization = adapter.Organization
            };
            adapter.PushSkillRunResponse("Got your message loud and clear!");
            var (user, _) = await adapter.CreateUser();
            var skillRepository = adapter.GetService<ISkillRepository>()!;
            await skillRepository.CreateAsync(skill, user);
            await adapter.SendTextToBotAndGetNextReplyAsync($"{botMention} alias add dog pug bomb");

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
                $"{botMention} dog");

            Assert.NotNull(reply);
            Assert.Equal("Got your message loud and clear!", reply.GetReplyMessageText());
        }

        [Theory]
        [InlineData(PlatformType.Slack, "U12345679", "<@U12345679> how are you?", "how are you?")]
        [InlineData(PlatformType.Slack, "U12345679", "<@U12345679> what're you up to?", "what're you up to?")]
        public async Task RespondsWithDefaultResponder(
            PlatformType platformType,
            string botUserId,
            string message,
            string inquiry)
        {
            var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);
            adapter.Organization.FallbackResponderEnabled = true;
            var skill = new Skill
            {
                Name = "pug",
                Language = CodeLanguage.CSharp,
                Organization = adapter.Organization
            };
            var (user, _) = await adapter.CreateUser();
            var skillRepository = adapter.GetService<ISkillRepository>()!;
            await skillRepository.CreateAsync(skill, user);

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync(message);

            Assert.NotNull(reply);
            Assert.Equal($"You want the answer to: {inquiry}", reply.GetReplyMessageText());
        }

        [Theory]
        [InlineData(PlatformType.Slack, "U12345679", "<@U12345679>", "<@U12345679>")]
        public async Task RespondsToAnEmptyMessageToAbbot(
            PlatformType platformType,
            string botUserId,
            string botMention,
            string expectedBotMention)
        {
            var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);
            var skill = new Skill
            {
                Name = "pug",
                Language = CodeLanguage.CSharp,
                Organization = adapter.Organization
            };
            var (user, _) = await adapter.CreateUser();
            var skillRepository = adapter.GetService<ISkillRepository>()!;
            await skillRepository.CreateAsync(skill, user);

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync(botMention);

            Assert.NotNull(reply);
            Assert.Equal($@"Sorry, I did not understand that. `{expectedBotMention} help` to learn what I can do.", reply.GetReplyMessageText());
        }
    }

    public class TheDebugMiddleware
    {
        [Theory]
        [InlineData(PlatformType.Slack, "U12345679", "<@U12345679>")]
        public async Task IgnoresNonDebugMessagesAndCallsNext(
            PlatformType platformType,
            string botUserId,
            string botMention)
        {
            var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync($"{botMention} echo test");

            Assert.NotNull(reply);
            Assert.Equal("test", reply.GetReplyMessageText());
        }

        [Theory]
        [InlineData(PlatformType.Slack, "U12345679", "<@U12345679>")]
        public async Task IgnoresDebugWhenItsNotEndOfStringMessagesAndCallsNext(
            PlatformType platformType,
            string botUserId,
            string botMention)
        {
            var adapter = await AbbotTestAdapter.CreateAsync(platformType, botUserId);

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
                $"{botMention} echo test --debug\nOtherStuff");

            Assert.NotNull(reply);
            Assert.Equal("test --debug\nOtherStuff", reply.GetReplyMessageText());
        }

        [Fact]
        public async Task PrependsDebugOfIncomingMessage()
        {
            var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack);
            adapter.Conversation.User.Id = "user1:abbot-brain";

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync(
                ".echo text --debug");

            Assert.NotNull(reply);
            var actual = reply.GetReplyMessageText();
            Assert.Contains(@"Abbot Debug Information (Received)", actual);
        }

        [Fact]
        public async Task AppendsDebugInfoToChannelDataForSlack()
        {
            var adapter = await AbbotTestAdapter.CreateAsync(PlatformType.Slack, "U12345678");

            var reply = await adapter.SendTextToBotAndGetNextReplyAsync("<@U12345678> echo test --debug");

            Assert.NotNull(reply);
            var channelData = Assert.IsType<MessageChannelData>(reply.ChannelData);
            var replyText = channelData.Message.Text;
            Assert.Contains(@"test

```
Abbot Debug Information (Sent)", replyText);
        }
    }

    public class TheMessageFormatMiddleware
    {
        [Fact]
        public async Task UsesBuiltInSlackMessageFormatterCorrectly()
        {
            var abbot = await AbbotTestAdapter.CreateAsync(PlatformType.Slack, "U12345678");

            var reply = await abbot.SendTextToBotAndGetNextReplyAsync("<@U12345678> echo `hello!`");

            Assert.NotNull(reply);
            Assert.Null(reply.Text);
            var channelData = Assert.IsType<MessageChannelData>(reply.ChannelData);
            Assert.Equal("`hello!`", channelData.Message.Text);
        }
    }
}

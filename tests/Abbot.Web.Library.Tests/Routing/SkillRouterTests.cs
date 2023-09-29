using Abbot.Common.TestHelpers;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Skills;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Serious.TestHelpers;

public class SkillRouterTests
{
    public class TheRetrieveSkillAsyncMethod
    {
        [Theory]
        [InlineData("\r\n<@U013WCHH9NU> skill1\n", "skill1", "")]
        [InlineData("<@U013WCHH9NU> skill1   something fierce", "skill1", "something fierce")]
        public async Task ReturnsBuiltInSkillAndArgumentsThatMatchesIncomingMessage(
            string message,
            string expectedSkill,
            string expectedArguments)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            var organization = env.TestData.Organization;
            organization.PlatformBotUserId = "U013WCHH9NU";
            await env.Db.SaveChangesAsync();
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: message,
                messageId: env.IdGenerator.GetSlackMessageId());
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            var testSkill = result.Skill as FakeSkill;
            Assert.NotNull(testSkill);
            Assert.Equal(expectedSkill, testSkill.Name);
            Assert.Equal(expectedSkill, result.Context.SkillName);
            Assert.Equal(expectedArguments, result.Context.Arguments.Value);
        }

        [Theory]
        [InlineData("skill1", "skill1", "")]
        [InlineData("skill1   something fierce", "skill1", "something fierce")]
        public async Task ReturnsRouteResultNotDirectedAtBotForIncomingDirectMessage(
            string message,
            string expectedSkill,
            string expectedArguments)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            await env.Db.SaveChangesAsync();
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: message,
                messageId: env.IdGenerator.GetSlackMessageId(),
                directMessage: true);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.False(result.IsDirectedAtBot);
        }

        [Fact]
        public async Task ReturnsRouteResultNotDirectedAtBotForWorkflowMessage()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            var organization = env.TestData.Organization;
            await env.Db.SaveChangesAsync();
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: $"<@{organization.PlatformBotUserId}> skill1",
                messageId: env.IdGenerator.GetSlackMessageId(),
                directMessage: false,
                workflowMessage: true);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.False(result.IsDirectedAtBot);
        }

        [Fact]
        public async Task ReturnsRouteResultWithNullSkillWhenSkillDoesNotMatch()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            var organization = env.TestData.Organization;
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: $"<@{organization.PlatformBotUserId}> unknown stuff",
                messageId: env.IdGenerator.GetSlackMessageId());
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Null(result.Skill);
            Assert.Equal("unknown", result.Context.SkillName);
            Assert.Equal("stuff", result.Context.Arguments.Value);
        }

        [Fact]
        public async Task RespondsToMessagesThatMentionBot()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            var organization = env.TestData.Organization;
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: $"Hey <@{organization.PlatformBotUserId}> skill1",
                messageId: env.IdGenerator.GetSlackMessageId());
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            var testSkill = result.Skill as FakeSkill;
            Assert.NotNull(testSkill);
            Assert.Equal("skill1", testSkill.Name);
            Assert.Equal("skill1", result.Context.SkillName);
        }

        [Theory]
        [InlineData("U013WCHH9NU", "Hey <@U013WCHH9NU> <@U012LKJFG0P> is a decent person", "U012LKJFG0P", "is <@U012LKJFG0P> a decent person")]
        [InlineData("U013WCHH9NU", "<@U013WCHH9NU> <@U012LKJFG0P> is a decent person", "U012LKJFG0P", "is <@U012LKJFG0P> a decent person")]
        [InlineData("U013WCHH9NU", "<@U013WCHH9NU> <@U02AG6QJP> is a fun guy", "U02AG6QJP", "is <@U02AG6QJP> a fun guy")]
        [InlineData("U013WCHH9NU", "<@U013WCHH9NU> who is <@U02AG6QJP>", "U02AG6QJP", "is <@U02AG6QJP>")]
        [InlineData("U013WCHH9NU", "Hey <@U013WCHH9NU> who is <@U02AG6QJP>", "U02AG6QJP", "is <@U02AG6QJP>")]
        public async Task SupportsMentionFirstPatternOfUserFollowedByIs(
            string platformBotUserId,
            string message,
            string platformUserId,
            string expectedArgs)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            organization.PlatformBotUserId = platformBotUserId;
            var member = env.TestData.Member;
            var user = member.User;
            user.PlatformUserId = platformUserId;
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            env.BuiltinSkillRegistry.AddSkill(env.Activate<WhoSkill>());
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: message,
                messageId: env.IdGenerator.GetSlackMessageId(),
                mentions: new[] { member });
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Equal("who", result.Context.SkillName);
            Assert.Equal(expectedArgs, result.Context.Arguments.Value);
            Assert.IsType<WhoSkill>(result.Skill);
        }

        [Theory]
        [InlineData("<@U013WCHH9NU> <@U123456789> can use ball", "U013WCHH9NU", "U123456789", "<@U123456789> use ball")]
        [InlineData("<@U013WCHH9NU> <@U987654321> can not ball", "U013WCHH9NU", "U987654321", "not <@U987654321> ball")]
        public async Task SupportsCanSkillPatternWithUserFollowedByCan(
            string message,
            string botUserId,
            string platformUserId,
            string expectedArgs)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            organization.PlatformBotUserId = botUserId;
            var member = env.TestData.Member;
            var user = member.User;
            user.PlatformUserId = platformUserId;
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            env.BuiltinSkillRegistry.AddSkill(env.Activate<WhoSkill>());
            env.BuiltinSkillRegistry.AddSkill(env.Activate<CanSkill>());
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: message,
                messageId: env.IdGenerator.GetSlackMessageId(),
                mentions: new[] { member });
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Equal("can", result.Context.SkillName);
            Assert.Equal(expectedArgs, result.Context.Arguments.Value);
            Assert.IsType<CanSkill>(result.Skill);
        }

        [Fact]
        public async Task DoesNotSupportMentionFirstWithUnknownVerb()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var user = member.User;
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill("skill1"));
            env.BuiltinSkillRegistry.AddSkill(env.Activate<WhoSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: $"<@{organization.PlatformBotUserId}> <@{user.PlatformUserId}> go hit dirt",
                messageId: env.IdGenerator.GetSlackMessageId(),
                mentions: new[] { member });
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Null(result.Skill);
        }

        [Theory]
        [InlineData("myalias", "original", "", "add")]
        [InlineData("myalias", "original", "list", "list add")]
        public async Task ReturnsSkillBasedOnAlias(
            string aliasName,
            string targetSkill,
            string targetArguments,
            string expectedArgs)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: $"<@{organization.PlatformBotUserId}> myalias add",
                messageId: env.IdGenerator.GetSlackMessageId());
            await env.CreateAliasAsync(aliasName, targetSkill, targetArguments);
            env.BuiltinSkillRegistry.AddSkill(new FakeSkill(targetSkill));
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            var testSkill = Assert.IsType<FakeSkill>(result.Skill);
            Assert.Equal("original", testSkill.Name);
            Assert.Equal("myalias", result.Context.SkillName);
            Assert.Equal(expectedArgs, result.Context.Arguments.Value);
        }

        [Fact]
        public async Task ReturnsSkill()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("pug");
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: $"<@{organization.PlatformBotUserId}> pug bomb",
                messageId: env.IdGenerator.GetSlackMessageId());
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.NotNull(result.Skill);
            Assert.IsType<RemoteSkillCallSkill>(result.Skill);
            Assert.Equal("pug", result.Context.SkillName);
            Assert.Equal("pug bomb", result.Context.Arguments.Value);
        }

        [Fact]
        public async Task ReturnsBuiltInSkill()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(env.Activate<ListSkill>());
            await env.CreateListAsync("deepthought", "Deep thoughts by Jack Handey");
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: $"<@{organization.PlatformBotUserId}> deepthought add A deep thought",
                messageId: env.IdGenerator.GetSlackMessageId());
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.NotNull(result.Skill);
            Assert.IsType<ListSkill>(result.Skill);
            Assert.Equal("deepthought", result.Context.SkillName);
            Assert.Equal("deepthought add A deep thought", result.Context.Arguments.Value);
        }

        [Fact]
        public async Task ReturnsSkillMatchedByPattern()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var pugSkill = await env.CreateSkillAsync("pug");
            pugSkill.Patterns.Add(new SkillPattern
            {
                Pattern = "pug",
                Name = "puggy-pug",
                PatternType = PatternType.Contains,
                Skill = pugSkill,
                Slug = "pug-slug"
            });
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: "anybody want a pug?",
                messageId: env.IdGenerator.GetSlackMessageId(),
                directMessage: false);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.NotNull(result.Skill);
            Assert.IsType<RemoteSkillCallSkill>(result.Skill);
            Assert.Equal(RemoteSkillCallSkill.SkillName, result.Context.SkillName);
            Assert.Equal("anybody want a pug?", result.Context.Arguments.Value);
            Assert.False(result.IsDirectedAtBot);
            Assert.True(result.IsPatternMatch);
        }

        [Fact]
        public async Task DoesNotReturnsSkillMatchedByPatternForForeignUser()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var pugSkill = await env.CreateSkillAsync("pug");
            pugSkill.Patterns.Add(new SkillPattern
            {
                Pattern = "pug",
                Name = "puggy-pug",
                PatternType = PatternType.Contains,
                Skill = pugSkill,
                Slug = "pug-slug"
            });
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: "anybody want a pug?",
                messageId: env.IdGenerator.GetSlackMessageId(),
                from: env.TestData.ForeignMember,
                directMessage: false);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Null(result.Skill);
        }

        [Fact]
        public async Task ReturnsSkillMatchedByPatternForForeignMemberWhenAllowExternalTrue()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var pugSkill = await env.CreateSkillAsync("pug");
            pugSkill.Patterns.Add(new SkillPattern
            {
                Pattern = "pug",
                Name = "puggy-pug",
                PatternType = PatternType.Contains,
                Skill = pugSkill,
                Slug = "pug-slug",
                AllowExternalCallers = true,
            });
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: "anybody want a pug?",
                messageId: env.IdGenerator.GetSlackMessageId(),
                from: env.TestData.ForeignMember,
                directMessage: false);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.NotNull(result.Skill);
            Assert.IsType<RemoteSkillCallSkill>(result.Skill);
            Assert.Equal(RemoteSkillCallSkill.SkillName, result.Context.SkillName);
            Assert.Equal("anybody want a pug?", result.Context.Arguments.Value);
            Assert.False(result.IsDirectedAtBot);
            Assert.True(result.IsPatternMatch);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task DoesNotReturnsSkillMatchedByPatternInDirectMessageNorWorkflowMessage(
            bool directMessage,
            bool workflowMessage)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var pugSkill = await env.CreateSkillAsync("pug");
            pugSkill.Patterns.Add(new SkillPattern
            {
                Pattern = "pug",
                Name = "puggy-pug",
                PatternType = PatternType.Contains,
                Skill = pugSkill,
                Slug = "pug-slug"
            });
            await env.Db.SaveChangesAsync();
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: "anybody want a pug?",
                messageId: env.IdGenerator.GetSlackMessageId(),
                directMessage: directMessage,
                workflowMessage: workflowMessage);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Null(result.Skill);
        }

        [Theory]
        [InlineData("U013WCHH9NU", "Hi everybody!", false)]
        [InlineData("U013WCHH9NU", ".this is for abbot", true)]
        [InlineData("U013WCHH9NU", "<@U013WCHH9NU> yo", true)]
        public async Task DetectsIfMessageIsDirectedAtAbbot(string botUserId, string message, bool isDirectedAtBot)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            organization.PlatformBotUserId = botUserId;
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: message,
                messageId: env.IdGenerator.GetSlackMessageId());
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Null(result.Skill);
            Assert.Equal(isDirectedAtBot, result.IsDirectedAtBot);
        }

        [Fact]
        public async Task DoesNotResolvesMatchingConversationIfThreadIdDoesNotMatchExists()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var room = await env.CreateRoomAsync();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            await env.CreateSkillAsync("pug");
            var remoteSkillCallSkill = env.Activate<RemoteSkillCallSkill>();
            env.BuiltinSkillRegistry.AddSkill(remoteSkillCallSkill);
            var router = env.Activate<SkillRouter>();
            var chatMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                message: $"<@{platformBotUserId} pug bomb",
                messageId: env.IdGenerator.GetSlackMessageId(),
                threadId: "123");

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.NotNull(result.Context);
            Assert.Null(result.Context.Conversation);
        }

        [Fact]
        public async Task ReturnsIgnoreRouteResultWhenCallerNotMemberOfOrganizationAndDirectedAtAbbot()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("pug");
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: $"<@{organization.PlatformBotUserId}> pug bomb",
                messageId: env.IdGenerator.GetSlackMessageId(),
                from: env.TestData.ForeignMember);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Same(RouteResult.Ignore, result);
        }

        [Fact]
        public async Task ReturnsResultWithNullSkillWhenCallerNotMemberOfOrganizationAndNotDirectedAtAbbot()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await env.CreateSkillAsync("pug");
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                message: "I could use some help here.",
                messageId: env.IdGenerator.GetSlackMessageId(),
                from: env.TestData.ForeignMember);
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.Null(result.Skill);
            Assert.False(result.IsDirectedAtBot);
        }

        [Fact]
        public async Task ReturnsResultWhenCallerNotMemberOfOrganizationButItIsAnInteractivePayload()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var skill = await env.CreateSkillAsync("pug");
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var chatMessage = env.CreatePlatformMessage(
                room,
                from: env.TestData.ForeignMember,
                payload: new MessageEventInfo(
                    "pug bomb",
                    "C001",
                    "U001",
                    Array.Empty<string>(),
                    true,
                    false,
                    env.IdGenerator.GetSlackMessageId(),
                    null,
                    new MessageInteractionInfo(
                        new MessageBlockActionsPayload
                        {
                            Message = new SlackMessage { Text = "pug bomb" },
                            Container = new MessageContainer("message_id", false, "C001")
                        },
                        "bomb",
                        new UserSkillCallbackInfo(skill, string.Empty)),
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    WorkflowMessage: false));
            var router = env.Activate<SkillRouter>();

            var result = await router.RetrieveSkillAsync(chatMessage);

            Assert.NotNull(result.Skill);
            Assert.IsType<RemoteSkillCallSkill>(result.Skill);
            Assert.True(result.IsDirectedAtBot);
            Assert.Equal("pug", result.Context.SkillName);
            Assert.Equal("pug bomb", result.Context.Arguments.Value);
        }
    }
}

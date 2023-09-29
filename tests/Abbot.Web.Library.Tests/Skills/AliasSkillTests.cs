using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Scripting;
using Serious.Abbot.Skills;
using Xunit;

public class AliasSkillTests
{
    public class AddingAnAlias
    {
        [Fact]
        public async Task CreatesAnAliasOnSlack()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var message = env.CreateFakeMessageContext("alias", "add bark-bark echo bow wow");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.StartsWith(
                $"Ok! `<@{organization.PlatformBotUserId}> bark-bark` will call `<@{organization.PlatformBotUserId}> echo bow wow`.",
                message.SingleReply());
            var alias = await env.Aliases.GetAsync("bark-bark", organization);
            Assert.NotNull(alias);
            Assert.Equal("echo", alias.TargetSkill);
            Assert.Equal("bow wow", alias.TargetArguments);
            Assert.Equal("A shortcut to `echo` that prepends `bow wow` to the arguments", alias.Description);
        }

        [Fact]
        public async Task CreatesAnAliasWithNoArgsWithDescription()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var message = env.CreateFakeMessageContext("alias", "add bark-bark echo");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.StartsWith(
                $"Ok! `<@{organization.PlatformBotUserId}> bark-bark` will call `<@{organization.PlatformBotUserId}> echo`.",
                message.SingleReply());
            var alias = await env.Aliases.GetAsync("bark-bark", organization);
            Assert.NotNull(alias);
            Assert.Equal("echo", alias.TargetSkill);
            Assert.Equal("", alias.TargetArguments);
            Assert.Equal("A shortcut to `echo`", alias.Description);
        }

        [Fact]
        public async Task RepliesWithMessageOnHowToAddDescriptionToAlias()
        {
            var env = TestEnvironment.Create();
            var botUserId = env.TestData.Organization.PlatformBotUserId;
            await env.CreateListAsync("deepthought");
            env.BuiltinSkillRegistry.AddSkill(env.Activate<ListSkill>());
            var message = env.CreateFakeMessageContext("alias", $"add thoughts {ListSkill.Name} deepthought");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"Ok! `<@{botUserId}> thoughts` will call `<@{botUserId}> list deepthought`.\nYou can add a description to the alias by calling `<@{botUserId}> alias describe thoughts {{description}}`",
                message.SingleReply());
        }

        [Fact]
        public async Task RejectsDuplicateAlias()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var user = env.TestData.User;
            var listSkill = env.Activate<ListSkill>();
            env.BuiltinSkillRegistry.AddSkill(listSkill);
            var message = env.CreateFakeMessageContext("alias", $"add deepthought {ListSkill.Name} deepthought");
            var aliasSkill = env.Activate<AliasSkill>();
            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            var reply = message.LastReply();
            Assert.StartsWith(
                $@"`deepthought` is already `{ListSkill.Name} deepthought` (added by <@{user.PlatformUserId}> on ",
                reply);
            Assert.EndsWith(
$"Try another name or call `<@{platformBotUserId}> alias remove deepthought` to remove it first.",
                reply);
        }

        [Fact]
        public async Task RejectsAliasThatConflictsWithBuiltInSkill()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var message = env.CreateFakeMessageContext("alias", "add echo stuff");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "The alias `echo` conflicts with the `echo` skill. Try another name.",
                message.SingleReply());
        }

        [Fact]
        public async Task RejectsAliasThatConflictsWithSkill()
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug");
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            env.BuiltinSkillRegistry.AddSkill(env.Activate<RemoteSkillCallSkill>());
            var message = env.CreateFakeMessageContext("alias", "add pug echo stuff");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "The alias `pug` conflicts with the `pug` skill. Try another name.",
                message.SingleReply());
        }

        [Fact]
        public async Task RejectsAliasThatDoesNotPointToAnExistingSkill()
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug");
            var message = env.CreateFakeMessageContext("alias", "add foo nonexistent args");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "Cannot create an alias to `nonexistent` as it does not exist.",
                message.SingleReply());
        }

        [Fact]
        public async Task RejectsAliasThatPointsToAnotherAlias()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            await env.CreateAliasAsync("bark", "echo", "bow wow");
            var message = env.CreateFakeMessageContext("alias", "add woof bark");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "Sorry, I cannot create an alias that points to another alias (`bark`).",
                message.SingleReply());
        }
    }

    public class RemoveAnAlias
    {
        [Theory]
        [InlineData("remove pickles")]
        [InlineData("delete pickles")]
        public async Task AllowsRemovingAnAlias(string arguments)
        {
            var env = TestEnvironment.Create();
            await env.CreateAliasAsync("pickles", "echo", "pickles");
            var message = env.CreateFakeMessageContext("alias", arguments);
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("Poof! I removed `pickles`.", message.SingleReply());
            Assert.Null(await env.Aliases.GetAsync("pickles", env.TestData.Organization));
        }
    }

    public class ListingAliases
    {
        [Fact]
        public async Task ShowsAllAliases()
        {
            var env = TestEnvironment.Create();
            await env.CreateAliasAsync("pickles", "echo", "pickles", "An alias to `echo` with the arguments `pickles`.");
            await env.CreateAliasAsync("porcupine", "echo", "porcupine", "An alias to `echo` with the arguments `porcupine`.");
            var message = env.CreateFakeMessageContext("alias", "list");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "• `remember` - A shortcut to `rem`.\n"
                + "• `pickles` - An alias to `echo` with the arguments `pickles`.\n"
                + "• `porcupine` - An alias to `echo` with the arguments `porcupine`.",
                message.SingleReply());
        }
    }

    public class AddingADescriptionToAnAlias
    {
        [Fact]
        public async Task RepliesWithAliasNotFound()
        {
            var env = TestEnvironment.Create();
            var message = env.CreateFakeMessageContext("alias", "describe pickles Calls the butter alias");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "I could not describe the alias `pickles` because it does not exist.",
                message.SingleReply());
        }

        [Fact]
        public async Task SetsTheDescription()
        {
            var env = TestEnvironment.Create();
            var alias = await env.CreateAliasAsync("pickles", "echo", "pickles");
            var message = env.CreateFakeMessageContext("alias", "describe pickles Calls the butter alias");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("Calls the butter alias", alias.Description);
            Assert.Equal(
                "I added the description to the alias.",
                message.SingleReply());
        }
    }

    public class ShowAlias
    {
        [Fact]
        public async Task ShowsDetailsAboutAnAlias()
        {
            var env = TestEnvironment.Create();
            await env.CreateAliasAsync("pickles", "echo", "pickles");
            var message = env.CreateFakeMessageContext("alias", "show pickles");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.StartsWith(
                $"`echo pickles` (added by <@{env.TestData.User.PlatformUserId}> on ",
                message.SingleReply());
        }

        [Fact]
        public async Task ShowsDetailsAndDescriptionForAlias()
        {
            var env = TestEnvironment.Create();
            await env.CreateAliasAsync("pickles", "echo", "pickles", "Useful");
            var message = env.CreateFakeMessageContext("alias", "show pickles");
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.StartsWith(
                $"`echo pickles` - Useful (added by <@{env.TestData.User.PlatformUserId}> on ",
                message.SingleReply());
        }
    }

    public class InvokingHelp
    {
        [Theory]
        [InlineData("")]
        [InlineData("help")]
        [InlineData("HELP")]
        public async Task ShowsUsageCorrectlyForSlack(string args)
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var message = env.CreateFakeMessageContext("alias", args);
            var aliasSkill = env.Activate<AliasSkill>();

            await aliasSkill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                $"`<@{platformBotUserId}> alias add {{name}} {{skill}} {{arguments}}`",
                message.SingleReply());
        }
    }
}

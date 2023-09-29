using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;
using Serious.Abbot.Skills;
using Serious.TestHelpers.CultureAware;
using Xunit;

public class CustomListSkillTests
{
    public class WithNoArgs
    {
        [Fact]
        public async Task ReturnsUsagePattern()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var message = env.CreateFakeMessageContext(ListSkill.Name);
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                $@"`<@{platformBotUserId}> list add {{name}} {{description}}` _creates a list",
                message.SingleReply());
        }
    }

    public class TheListListCommand
    {
        [Fact]
        public async Task ListsAllLists()
        {
            var env = TestEnvironment.Create();
            await env.CreateListAsync("deepthought", "Deep thoughts");
            await env.CreateListAsync("quotes");
            await env.CreateListAsync("pmnisms");
            var message = env.CreateFakeMessageContext(ListSkill.Name, "list");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "• `joke` - Abbot's favorite jokes.\n" +
                "• `deepthought` - Deep thoughts.\n" +
                "• `quotes`\n" +
                "• `pmnisms`",
                message.SingleReply());
        }

        [Fact]
        public async Task RepliesWithUsageWhenNoListItemsExist()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var allLists = await env.Db.UserLists.ToListAsync();
            env.Db.UserLists.RemoveRange(allLists);
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(ListSkill.Name, "list");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There are no lists yet. Use `<@{platformBotUserId}> list add {{name}} {{description}}` to add a list. Use `<@{platformBotUserId}> help list` to see how to use the `list` command",
                message.SingleReply());
        }
    }

    public class TheRetrieveRandomItemFromListOperation
    {
        [Fact]
        public async Task WithNoMatchingListReturnsUsageMessage()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var message = env.CreateFakeMessageContext(ListSkill.Name, "deepthought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"The list `deepthought` doesn’t exist. Use `<@{platformBotUserId}> list add deepthought {{description}}` to create the list.",
                message.SingleReply());
        }

        [Fact]
        public async Task WithNoItemsReturnsUsageMessage()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            await env.CreateListAsync("deepthought");
            var message = env.CreateFakeMessageContext(ListSkill.Name, "deepthought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"The list `deepthought` is empty. Use `<@{platformBotUserId}> deepthought add {{something}}` to add items to this list.",
                message.SingleReply());
        }

        [Fact]
        public async Task ReturnsRandomListItemFromMatchingList()
        {
            var env = TestEnvironment.Create();
            var list = await env.CreateListAsync("deepthought");
            list.Entries.Add(new UserListEntry { Content = "A Very Deep Thought", List = list });
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(ListSkill.Name, "deepthought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("A Very Deep Thought", message.SingleReply());
        }
    }

    public class TheCreatingAListOperation
    {
        [Fact]
        public async Task CreatesAListWithName()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var messageContext = env.CreateFakeMessageContext("list", "add deep-thought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                $"Created list `deep-thought`. Use `<@{platformBotUserId}> deep-thought add {{something}}` to add items to this" +
                $" list. Use `<@{platformBotUserId}> list describe deep-thought {{description}}` to add a description so others know what the list is about.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task ReturnsErrorWhenNameConflictsWithBuiltInSkill()
        {
            var env = TestEnvironment.Create();
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var messageContext = env.CreateFakeMessageContext("list", "add echo");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "The name `echo` is already in use.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task CreatesAListWithNameAndDescription()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var messageContext = env.CreateFakeMessageContext("list", "add deepthought Deep thoughts by Jack Handey");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                $"Created list `deepthought`. Use `<@{platformBotUserId}> deepthought add {{something}}` to add items to this list.",
                messageContext.SentMessages.Single());
            var list = await env.Lists.GetAsync("deepthought", env.TestData.Organization);
            Assert.NotNull(list);
            Assert.Equal("Deep thoughts by Jack Handey", list.Description);
        }
    }

    public class TheListRemoveOperation
    {
        [Fact]
        public async Task WarnsNoListToRemove()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("list", "remove deepthought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal("Nothing to remove. The list `deepthought` doesn’t exist.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task RemovesAListByName()
        {
            var env = TestEnvironment.Create();
            await env.CreateListAsync("deepthought");
            var messageContext = env.CreateFakeMessageContext("list", "remove deepthought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "Poof! I removed the list `deepthought`.",
                messageContext.SentMessages.Single());
        }
    }

    public class TheInfoCommand
    {
        [Fact]
        [UseCulture("en-us")]
        public async Task ShowsInfoAboutAList()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            await env.CreateListAsync("deepthought", "Description");
            var messageContext = env.CreateFakeMessageContext("list", "deepthought info");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.StartsWith(
                $"The list `deepthought` with description \"_Description_\" was created by `<@{user.PlatformUserId}>` on ",
                reply);
            Assert.EndsWith(" and has `0` items.", reply);
        }
    }

    public class TheListCommandForAList
    {
        [Fact]
        public async Task ReturnsUsageWhenNoItemsInList()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            await env.CreateListAsync("deepthought");
            var messageContext = env.CreateFakeMessageContext("list", "deepthought list");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                $"There are no items in the list `deepthought`. Try `<@{platformBotUserId}> deepthought add ...` to add something to the list.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task ListsAllItemsInList()
        {
            var env = TestEnvironment.Create();
            var list = await env.CreateListAsync("deepthought");
            list.Entries = new List<UserListEntry>
            {
                new() {Content = "one", List = list },
                new() {Content = "two", List = list },
                new() {Content = "three", List = list }
            };
            await env.Db.SaveChangesAsync();
            var messageContext = env.CreateFakeMessageContext("list", "deepthought list");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal("• `one`\n• `two`\n• `three`", messageContext.SentMessages.Single());
        }
    }

    public class TheAddingItemsToListOperation
    {
        [Fact]
        public async Task AddsItemToList()
        {
            var env = TestEnvironment.Create();
            await env.CreateListAsync("deepthought");
            var messageContext = env.CreateFakeMessageContext(
                "list",
                "deepthought add This is a Deep Thought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "Added `This is a Deep Thought` to the `deepthought` list.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task WarnsItemDoesNotExist()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var messageContext = env.CreateFakeMessageContext(
                "list",
                "deepthought add This is a Deep Thought");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                $"The list `deepthought` doesn’t exist. Use `<@{platformBotUserId}> list add deepthought {{description}}` to create the list.",
                messageContext.SentMessages.Single());
        }
    }

    public class TheUnknownListOperation
    {
        [Fact]
        public async Task ShowsRandomItemForExtraArguments()
        {
            var env = TestEnvironment.Create();
            var list = await env.CreateListAsync("deepthought");
            list.Entries.Add(new UserListEntry { Content = "A Very Deep Thought", List = list });
            list.Entries.Add(new UserListEntry { Content = "A Very Deep Thought", List = list });
            var messageContext = env.CreateFakeMessageContext(
                "list",
                "deepthought farfegnugen");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal("A Very Deep Thought", messageContext.SentMessages.Single());
        }
    }

    public class TheAddingDescriptionToListOperation
    {
        [Fact]
        public async Task CanUpdateListDescription()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var list = await env.CreateListAsync("deepthought");
            list.Entries.Add(new UserListEntry { Content = "A Very Deep Thought", List = list });
            list.Entries.Add(new UserListEntry { Content = "A Very Deep Thought", List = list });
            var messageContext = env.CreateFakeMessageContext(
                "list",
                "describe deepthought Deep Thoughts");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "I updated the description for the `deepthought` list.",
                messageContext.SentMessages.Single());
            var retrieved = await env.Lists.GetAsync("deepthought", organization);
            Assert.NotNull(retrieved);
            Assert.Equal("Deep Thoughts", retrieved.Description);
        }

        [Fact]
        public async Task ShowsErrorWhenListDoesNotExist()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext(
                "list",
                "describe deepthought Deep Thoughts");
            var skill = env.Activate<ListSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "Nothing to describe. The list `deepthought` doesn’t exist.",
                messageContext.SentMessages.Single());
        }
    }
}

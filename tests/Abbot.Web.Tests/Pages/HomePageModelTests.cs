using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Xunit;

public class HomePageModelTests
{
    public class TheOnGetAsyncMethod : PageTestBase<HomePageModel>
    {
        [Fact]
        public async Task RedirectsToAccountSettingsWithoutRoles()
        {
            Assert.Empty(Env.TestData.Member.MemberRoles);

            var (_, result) = await InvokePageAsync<RedirectToPageResult>(p => p.OnGetAsync(null, null, null));
            Assert.Equal("/Settings/Account/Index", result.PageName);
        }

        [Fact]
        public async Task RedirectsToAccountSettingsIfCannotManageConversations()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Staff, Env.TestData.Abbot);

            var (_, result) = await InvokePageAsync<RedirectToPageResult>(p => p.OnGetAsync(null, null, null));
            Assert.Equal("/Settings/Account/Index", result.PageName);
        }

        [Theory]
        [InlineData(Roles.Agent)]
        [InlineData(Roles.Administrator)]
        public async Task ShowsPageWhenConversationsExistAndBotInstalled(string role)
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, role, Env.TestData.Abbot);
            Env.TestData.Organization.PlatformBotId = "SomeBotId"; // Sets that the bot is installed.
            await Env.Db.SaveChangesAsync();
            var room = await Env.CreateRoomAsync(managedConversationsEnabled: false);
            await Env.CreateConversationAsync(room);

            var (_, result) = await InvokePageAsync(p => p.OnGetAsync(null, null, null));

            Assert.True(await Env.Conversations.HasAnyConversationsAsync(Env.TestData.Organization));
            Assert.True(Env.TestData.Organization.IsBotInstalled());
            Assert.IsType<PageResult>(result);
        }

        [Theory]
        [InlineData(Roles.Agent)]
        [InlineData(Roles.Administrator)]
        public async Task ShowsPageWhenEnabledRoomsExistAndBotInstalled(string role)
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, role, Env.TestData.Abbot);
            Env.TestData.Organization.PlatformBotId = "SomeBotId"; // Sets that the bot is installed.
            await Env.Db.SaveChangesAsync();
            await Env.CreateRoomAsync(managedConversationsEnabled: true);

            var (page, result) = await InvokePageAsync(p => p.OnGetAsync(null, null, null));

            Assert.Empty(page.Conversations);
            Assert.True(Env.TestData.Organization.IsBotInstalled());
            Assert.IsType<PageResult>(result);
        }
    }

    public class TheOnGetAsyncMethodWithConversations : ConversationPageTestBase<HomePageModel>
    {
        [Fact]
        public async Task WhenNonExistentRoomSpecified_ReturnsNotFound()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var (_, result) = await InvokePageAsync(p =>
                p.OnGetAsync(null, "9999", null));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task WhenRoomInOtherOrgSpecified_ReturnsNotFound()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var (_, result) = await InvokePageAsync(p =>
                p.OnGetAsync(null, $"{TestRoom2.Id}", null));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task WhenStatusFilterNotProvided_LoadsOverdueConversationsForOrganization()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var (page, result) = await InvokePageAsync(p =>
                p.OnGetAsync(null, null, null));

            Assert.IsType<PageResult>(result);
            Assert.Equal(new[] { "Convo2", "Convo0" }, page.Conversations.Select(c => c.Title).ToArray());
        }

        [Fact]
        public async Task WhenStatusFilterIsOtherString_LoadsConversationsForOrgMatchingStatus()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var (page, result) = await InvokePageAsync(p =>
                p.OnGetAsync(ConversationStateFilter.NeedsResponse, null, null));

            Assert.IsType<PageResult>(result);
            Assert.Equal(new[] { "Convo2", "Convo0" }, page.Conversations.Select(c => c.Title).ToArray());
        }

        [Fact]
        public async Task WhenRoomIdProvided_LoadsConversationsAndStatsForRoom()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);
            var secondRoom = await Rooms.CreateAsync(new Room()
            {
                Name = "second-room",
                OrganizationId = TestOrganization.Id,
                PlatformRoomId = "C0002",
            });

            secondRoom.TimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));
            var convo = await Conversations.CreateAsync(
                secondRoom,
                new MessagePostedEvent
                {
                    MessageId = "000A",
                    MessageUrl = new Uri("https://example.com/messages/A")
                },
                "ConvoA",
                TestMember,
                DateTime.UtcNow.AddDays(-1),
                null);

            var (page, result) = await InvokePageAsync(p =>
                p.OnGetAsync(ConversationStateFilter.All, $"{secondRoom.Id}", null));

            Assert.IsType<PageResult>(result);
            Assert.Equal(new[] { convo.Id }, page.Conversations.Select(c => c.Id).ToArray());
        }

        [Fact]
        public async Task WhenMyProvided_LoadsConversationsAndStatsUserIsResponderFor()
        {
            await Env.Roles.AddUserToRoleAsync(TestMember, Roles.Agent, Env.TestData.Abbot);
            await Env.Roles.AddUserToRoleAsync(TestMember2, Roles.Agent, Env.TestData.Abbot);
            await Env.Organizations.AssignDefaultFirstResponderAsync(TestOrganization, TestMember, Env.TestData.Abbot);
            var roomMemberIsFirstResponder = await Env.CreateRoomAsync();
            var roomHasAnotherFirstResponder = await Env.CreateRoomAsync();
            var roomWithNoFirstResponderAndMemberIsDefaultResponder = await Env.CreateRoomAsync();
            var roomMemberIsEscalationResponder = await Env.CreateRoomAsync();
            await Env.Rooms.AssignMemberAsync(roomMemberIsFirstResponder, TestMember, RoomRole.FirstResponder, Env.TestData.Abbot);
            await Env.Rooms.AssignMemberAsync(roomMemberIsEscalationResponder, TestMember, RoomRole.EscalationResponder, Env.TestData.Abbot);
            await Env.Rooms.AssignMemberAsync(roomHasAnotherFirstResponder, TestMember2, RoomRole.FirstResponder, Env.TestData.Abbot);
            await Env.Rooms.AssignMemberAsync(roomWithNoFirstResponderAndMemberIsDefaultResponder, TestMember2, RoomRole.EscalationResponder, Env.TestData.Abbot);
            await Env.Rooms.AssignMemberAsync(TestRoom, TestMember2, RoomRole.FirstResponder, Env.TestData.Abbot);
            await Env.Rooms.AssignMemberAsync(TestRoom2, TestMember2, RoomRole.FirstResponder, Env.TestData.Abbot);
            TestRoom.TimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));
            TestRoom2.TimeToRespond = TestRoom.TimeToRespond;
            TestMember.IsDefaultFirstResponder = true;
            await Env.Db.SaveChangesAsync();
            var convoInFirstResponderRoom = await Conversations.CreateAsync(
                roomMemberIsFirstResponder,
                new MessagePostedEvent
                {
                    MessageId = "000A",
                    MessageUrl = new Uri("https://example.com/messages/A")
                },
                "ConvoA",
                TestMember,
                DateTime.UtcNow.AddDays(-1),
                null);
            await Conversations.CreateAsync(
                roomHasAnotherFirstResponder,
                new MessagePostedEvent
                {
                    MessageId = "000B",
                    MessageUrl = new Uri("https://example.com/messages/B")
                },
                "ConvoB",
                TestMember,
                DateTime.UtcNow.AddDays(-1),
                null);
            var convoInRoomWithNoFirstResponder = await Conversations.CreateAsync(
                roomWithNoFirstResponderAndMemberIsDefaultResponder,
                new MessagePostedEvent
                {
                    MessageId = "000C",
                    MessageUrl = new Uri("https://example.com/messages/C")
                },
                "ConvoC",
                TestMember,
                DateTime.UtcNow.AddDays(-1),
                null);
            var convoInEscalationResponderRoom = await Conversations.CreateAsync(
                roomMemberIsEscalationResponder,
                new MessagePostedEvent
                {
                    MessageId = "000D",
                    MessageUrl = new Uri("https://example.com/messages/D")
                },
                "ConvoD",
                TestMember,
                DateTime.UtcNow.AddDays(-1),
                null);

            var (page, result) = await InvokePageAsync(p =>
                p.OnGetAsync(ConversationStateFilter.All, $"my", null));

            Assert.IsType<PageResult>(result);
            Assert.Equal(
                expected: new[]
                {
                    convoInEscalationResponderRoom.Id,
                    convoInRoomWithNoFirstResponder.Id,
                    convoInFirstResponderRoom.Id,
                },
                actual: page.Conversations.Select(c => c.Id).ToArray());
        }
    }
}

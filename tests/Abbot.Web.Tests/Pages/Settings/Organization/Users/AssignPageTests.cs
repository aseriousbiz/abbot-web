
using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using AngleSharp.Text;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Settings.Organization.Users;
using Serious.Abbot.Security;
using Xunit;

public class AssignPageTests
{
    public class TheOnGetAsyncMethod : PageTestBase<AssignPage>
    {
        [Theory]
        [InlineData(Roles.Agent, Roles.Administrator)]
        [InlineData(Roles.Agent)]
        [InlineData(Roles.Administrator)]
        public async Task LoadsExistingRolesForUser(params string[] roles)
        {
            var actor = Env.TestData.Member;
            await Env.Roles.AddUserToRoleAsync(actor, Roles.Administrator, Env.TestData.Abbot);
            var subject = await Env.CreateMemberAsync();
            foreach (var role in roles)
            {
                await Env.Roles.AddUserToRoleAsync(subject, role, Env.TestData.Abbot);
            }

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync(subject.User.PlatformUserId));

            bool expectAgent = roles.Contains(Roles.Agent);
            bool expectAdministrator = roles.Contains(Roles.Administrator);
            Assert.Collection(page.Roles,
                r => Assert.Equal((Roles.Agent, expectAgent), (r.Value, r.Selected)),
                r => Assert.Equal((Roles.Administrator, expectAdministrator), (r.Value, r.Selected)));
        }
    }

    public class TheOnPostAsyncMethod : PageTestBase<AssignPage>
    {
        [Fact]
        public async Task ReturnsNotFoundIfNoMemberWithId()
        {
            var (_, result) = await InvokePageAsync(p => p.OnPostAsync(NonExistent.PlatformUserId));
            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(Roles.Administrator, Roles.Agent)]
        [InlineData(Roles.Agent)]
        [InlineData(Roles.Administrator)]
        [InlineData]
        public async Task AddsUserToAppropriateRoles(params string[] roles)
        {
            var actor = Env.TestData.Member;
            await Env.Roles.AddUserToRoleAsync(actor, Roles.Administrator, Env.TestData.Abbot);
            var subject = await Env.CreateMemberAsync();
            Assert.False(subject.IsAgent());
            Assert.False(subject.IsAdministrator());

            var (_, result) = await InvokePageAsync(async p => {
                p.SelectedRoles = roles;
                return await p.OnPostAsync(subject.User.PlatformUserId);
            });


            Assert.IsType<RedirectToPageResult>(result);
            await Env.ReloadAsync(subject);
            bool expectedAgent = roles.Contains(Roles.Agent);
            bool expectedAdmin = roles.Contains(Roles.Administrator);
            Assert.Equal(expectedAgent, subject.IsAgent());
            Assert.Equal(expectedAdmin, subject.IsAdministrator());
        }

        [Fact]
        public async Task RemovesUserFromRolesAndRoomRolesWhenChoosingNoRoles()
        {
            var actor = Env.TestData.Member;
            var room = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            await Env.Roles.AddUserToRoleAsync(actor, Roles.Administrator, Env.TestData.Abbot);
            var subject = await Env.CreateMemberInAgentRoleAsync();
            await Env.Rooms.AssignMemberAsync(room, subject, RoomRole.EscalationResponder, Env.TestData.Member);
            await Env.Rooms.AssignMemberAsync(room, subject, RoomRole.FirstResponder, Env.TestData.Member);
            await Env.Roles.AddUserToRoleAsync(subject, Roles.Administrator, Env.TestData.Abbot);
            Assert.True(subject.IsAgent());
            Assert.True(subject.IsAdministrator());

            var (_, result) = await InvokePageAsync(async p => {
                p.SelectedRoles = Array.Empty<string>();
                return await p.OnPostAsync(subject.User.PlatformUserId);
            });

            Assert.IsType<RedirectToPageResult>(result);
            await Env.ReloadAsync(subject);
            Assert.False(subject.IsAgent());
            Assert.False(subject.IsAdministrator());
            Assert.NotNull(subject.RoomAssignments);
            Assert.Empty(subject.RoomAssignments);
        }

        [Fact]
        public async Task DoesNotAllowRemovingSelfFromAdministrator()
        {
            var actor = Env.TestData.Member;
            await Env.Roles.AddUserToRoleAsync(actor, Roles.Administrator, Env.TestData.Abbot);
            Assert.True(actor.IsAdministrator());

            var (page, result) = await InvokePageAsync(async p => {
                p.SelectedRoles = Array.Empty<string>();
                return await p.OnPostAsync(actor.User.PlatformUserId);
            });

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal($"{WebConstants.ErrorStatusPrefix}You cannot remove your own Administrator role.", page.StatusMessage);
        }
    }
}

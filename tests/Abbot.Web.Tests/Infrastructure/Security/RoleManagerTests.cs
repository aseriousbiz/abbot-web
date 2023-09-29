using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Security;
using Serious.TestHelpers;
using Xunit;

public class RoleManagerTests
{
    public class TheAddUserToRoleAsyncMethod
    {
        [Fact]
        public async Task AllowsAbbotToAddInstallingUserToAdminRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var abbot = env.TestData.Abbot;
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.AddUserToRoleAsync(member, Roles.Administrator, abbot);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.False(env.BackgroundSlackClient.EnqueueAdminWelcomeMessageCalled);
            Assert.True(env.BackgroundSlackClient.EnqueueMessageToInstallerCalled);
        }

        [Fact]
        public async Task AllowsAdminToAddUserToAdminRoleInDatabaseAndApi()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var adminMember = await env.CreateAdminMemberAsync();
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.AddUserToRoleAsync(member, Roles.Administrator, adminMember);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.True(env.BackgroundSlackClient.EnqueueAdminWelcomeMessageCalled);
            Assert.False(env.BackgroundSlackClient.EnqueueMessageToInstallerCalled);
        }

        [Fact]
        public async Task DoesNotAllowArchivedAdminToAddUserToAdminRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var adminMember = await env.CreateAdminMemberAsync();
            adminMember.Active = false;
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.AddUserToRoleAsync(
                member,
                Roles.Agent,
                adminMember));
        }

        [Fact]
        public async Task AllowsStaffToAddUserToStaffRoleInDatabaseAndApi()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var staffMember = await env.CreateStaffMemberAsync();
            var roleManager = env.Activate<RoleManager>();
            Assert.True(staffMember.IsStaff());

            await roleManager.AddUserToRoleAsync(member, Roles.Staff, staffMember);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Staff);
            Assert.False(env.BackgroundSlackClient.EnqueueAdminWelcomeMessageCalled);
        }

        [Fact]
        public async Task PublishesMessageWhenAddingToAgentRole()
        {
            var env = TestEnvironment.Create();
            var subject = await env.CreateMemberAsync();
            var staffMember = await env.CreateStaffMemberAsync();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.AddUserToRoleAsync(subject, Roles.Agent, staffMember);

            Assert.Contains(subject.MemberRoles, ur => ur.Role.Name == Roles.Agent);
        }

        [Fact]
        public async Task AllowsAbbotMemberToAddUserToMemberRoleInDatabaseAndApi()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var abbotMember = env.TestData.Abbot;
            var roleManager = env.Activate<RoleManager>();

            await roleManager.AddUserToRoleAsync(member, Roles.Agent, abbotMember);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            Assert.False(env.BackgroundSlackClient.EnqueueAdminWelcomeMessageCalled);
        }

        [Fact]
        public async Task ThrowsExceptionWhenNonAdminAttemptsToAddUserToRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var anotherMember = await env.CreateMemberInAgentRoleAsync();
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.AddUserToRoleAsync(
                member,
                Roles.Agent,
                anotherMember));
        }

        [Fact]
        public async Task ThrowsExceptionWhenAbbotAttemptsToAddUserToStaffRole()
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IHostEnvironment>(new FakeHostEnvironment { EnvironmentName = "Production" })
                .Build();
            var member = await env.CreateMemberInAgentRoleAsync();
            var abbotMember = env.TestData.Abbot;
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.AddUserToRoleAsync(
                member,
                Roles.Staff,
                abbotMember));
        }

        [Fact]
        public async Task AllowsAbbotToAddUserToStaffRoleInDevelopment()
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IHostEnvironment>(new FakeHostEnvironment { EnvironmentName = "Development" })
                .Build();
            var member = await env.CreateMemberInAgentRoleAsync();
            var abbotMember = env.TestData.Abbot;
            var roleManager = env.Activate<RoleManager>();

            await roleManager.AddUserToRoleAsync(
                member,
                Roles.Staff,
                abbotMember);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Staff);
        }
    }

    public class TheRemoveUserFromRoleAsyncMethod
    {
        [Fact]
        public async Task RemovesUserFromRoleInDatabaseAndApi()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateAdminMemberAsync();
            var adminMember = await env.CreateAdminMemberAsync();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.RemoveUserFromRoleAsync(member, Roles.Administrator, adminMember);

            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
        }

        [Fact]
        public async Task AllowsAbbotMemberToRemoveUserFromRoleInDatabaseAndApi()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateAdminMemberAsync();
            var abbotMember = env.TestData.Abbot;
            var roleManager = env.Activate<RoleManager>();

            await roleManager.RemoveUserFromRoleAsync(member, Roles.Administrator, abbotMember);

            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
        }

        [Fact]
        public async Task ThrowsExceptionWhenNonAdminAttemptsToRemoveUserFromRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var anotherMember = await env.CreateMemberInAgentRoleAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.RemoveUserFromRoleAsync(
                member,
                Roles.Administrator,
                anotherMember));
        }

        [Fact]
        public async Task ThrowsExceptionWhenNonActiveUserAttemptsToRemoveUserFromStaffRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var adminMember = await env.CreateAdminMemberAsync();
            adminMember.Active = false;
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.RemoveUserFromRoleAsync(
                member,
                Roles.Staff,
                adminMember));
        }

        [Fact]
        public async Task ThrowsExceptionWhenNonStaffAttemptsToRemoveUserFromStaffRole()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var anotherMember = await env.CreateAdminMemberAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.RemoveUserFromRoleAsync(
                member,
                Roles.Staff,
                anotherMember));
        }
    }

    public class TheSyncRolesFromListAsyncMethod
    {
        [Fact]
        public async Task AllowsAdminMemberToAddUserToAdministratorAndRemoveFromMember()
        {
            var env = TestEnvironment.Create();
            var staff = await env.CreateStaffMemberAsync();
            var member = await env.CreateMemberInAgentRoleAsync();
            await env.Roles.AddUserToRoleAsync(member, Roles.Staff, staff);
            var adminMember = await env.CreateAdminMemberAsync();
            var newRoles = new[] { Roles.Administrator, Roles.Staff };
            var roleManager = env.Activate<RoleManager>();

            await roleManager.SyncRolesFromListAsync(member, newRoles, adminMember);

            Assert.Collection(member.MemberRoles,
                role0 => Assert.Equal(Roles.Staff, role0.Role.Name),
                role1 => Assert.Equal(Roles.Administrator, role1.Role.Name));
        }

        [Fact]
        public async Task DoesNothingIfNoChanges()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateAdminMemberAsync();
            await env.Roles.AddUserToRoleAsync(member, Roles.Agent, member);
            var memberNoRoles = await env.CreateMemberAsync();
            var newRoles = new[] { Roles.Agent, Roles.Administrator };
            var roleManager = env.Activate<RoleManager>();

            await roleManager.SyncRolesFromListAsync(member, newRoles, memberNoRoles);

            Assert.Collection(member.MemberRoles,
                role0 => Assert.Equal(Roles.Administrator, role0.Role.Name),
                role1 => Assert.Equal(Roles.Agent, role1.Role.Name));
        }

        [Fact]
        public async Task ThrowsExceptionWhenNonStaffAttemptsToRemoveUserFromStaffRole()
        {
            var env = TestEnvironment.Create();
            var staff = await env.CreateStaffMemberAsync();
            var anotherMember = await env.CreateAdminMemberAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.SyncRolesFromListAsync(
                staff,
                new[] { Roles.Agent },
                anotherMember));
        }
    }

    public class TheUpdateCurrentUserRolesAsyncMethod
    {
        [Fact]
        public async Task TheAddRolesToPrincipalMethod()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, member.User.NameIdentifier!) }));
            var roleManager = env.Activate<RoleManager>();

            roleManager.SyncRolesToPrincipal(member, principal);

            var userRoles = member.MemberRoles
                .Select(ur => ur.Role)
                .OrderBy(r => r.Name)
                .ToList();
            Assert.Single(userRoles);
            Assert.Equal("Agent", userRoles[0].Name);
            var roleClaims = principal.GetRoleClaimValues().ToList();
            Assert.Single(roleClaims);
            Assert.Equal("Agent", roleClaims[0]);
        }

        [Fact]
        public async Task DoesNotAddRolesForInactiveMembers()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            member.Active = false;
            await env.Db.SaveChangesAsync();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, member.User.NameIdentifier!) }));
            var roleManager = env.Activate<RoleManager>();

            roleManager.SyncRolesToPrincipal(member, principal);

            // Make sure member still has Agent role.
            var userRole = Assert.Single(member.MemberRoles
                .Select(ur => ur.Role)
                .OrderBy(r => r.Name)
                .ToList());
            Assert.Equal(Roles.Agent, userRole.Name);
            var roleClaims = principal.GetRoleClaimValues().ToList();
            // But make sure the principal does not have claims.
            Assert.Empty(roleClaims);
        }
    }

    public class TheCreateRoleAsyncMethod
    {
        [Fact]
        public async Task CreatesRoleInApiAndDatabase()
        {
            var env = TestEnvironment.CreateWithoutData();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.CreateRoleAsync("Test", "A Test Role");

            var role = Assert.Single(await env.Db.Roles.ToListAsync());
            Assert.Equal("Test", role.Name);
            Assert.Equal("A Test Role", role.Description);
        }

        [Fact]
        public async Task CreatesRoleInDatabaseButNotApiIfExistsInApi()
        {
            var env = TestEnvironment.CreateWithoutData();
            var roleManager = env.Activate<RoleManager>();

            var result = await roleManager.CreateRoleAsync(
                Roles.Administrator,
                "A Test Role");

            var dbRoles = await env.Db.Roles.ToListAsync();
            var role = Assert.Single(dbRoles);
            Assert.Equal("Administrator", role.Name);
            Assert.Equal(result.Id, role.Id);
        }

        [Fact]
        public async Task CreatesRoleInApiButNotDatabaseIfExistsInDatabase()
        {
            var env = TestEnvironment.CreateWithoutData();
            var dbRole = new Role
            {
                Name = Roles.Administrator,
                Description = "The Admins"
            };
            await env.Db.Roles.AddAsync(dbRole);
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            var result = await roleManager.CreateRoleAsync(
                Roles.Administrator,
                "A Test Role");

            Assert.Same(result, dbRole);
            var dbRoles = await env.Db.Roles.ToListAsync();
            var role = Assert.Single(dbRoles);
            Assert.Equal("Administrator", result.Name);
            Assert.Equal(result.Id, role.Id);
        }
    }

    public class TheRestoreMemberAsyncMethod
    {
        [Fact]
        public async Task AllowsAdminToRestoreMember()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberAsync();
            member.Active = false;
            await env.Db.SaveChangesAsync();
            Assert.Empty(member.MemberRoles);
            var adminMember = await env.CreateAdminMemberAsync();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.RestoreMemberAsync(member, adminMember);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            Assert.True(member.Active);
        }

        [Fact]
        public async Task SetsUserActiveIfAlreadyInMembersRole()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            Assert.Contains(agent.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            agent.Active = false;
            await env.Db.SaveChangesAsync();
            var adminMember = await env.CreateAdminMemberAsync();
            var roleManager = env.Activate<RoleManager>();

            await roleManager.RestoreMemberAsync(agent, adminMember);

            Assert.Contains(agent.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            Assert.True(agent.Active);
        }

        [Fact]
        public async Task ThrowsExceptionWhenNonAdminTriesToRestoreUser()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var nonAdmin = await env.CreateMemberInAgentRoleAsync();
            await env.Db.SaveChangesAsync();
            var roleManager = env.Activate<RoleManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => roleManager.RestoreMemberAsync(
                member,
                nonAdmin));
        }
    }
}

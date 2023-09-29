using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Serious.Abbot.Security;
using Serious.TestHelpers;

public class AuthenticationHandlerTests
{
    public class TheHandleAuthenticatedUserAsyncMethod
    {
        [Fact]
        public async Task WhenOrgDoesNotExistCreatesItAndSendsActivationAnalytics()
        {
            var env = TestEnvironment.Create();
            var principal = new FakeClaimsPrincipal(
                platformId: "the-a-team-id",
                platformUserId: "U012345",
                platformTeamName: "The A Team",
                domain: "a.example.com");
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            var organization = await env.Organizations.GetAsync(principal.GetPlatformTeamId().Require());
            Assert.NotNull(organization);
            Assert.Equal("the-a-team-id", organization.PlatformId);
            Assert.Equal("The A Team", organization.Name);
            Assert.Equal("a.example.com", organization.Domain);
            var member = await env.Db.Members.Where(m => m.User.PlatformUserId == "U012345").FirstAsync();
            env.AnalyticsClient.AssertTracked(
                "Organization activated",
                AnalyticsFeature.Activations,
                member);
            Assert.True(env.BackgroundSlackClient.EnqueueUpdateOrganizationCalled);
        }

        [Fact]
        public async Task WhenOrgExistsButAbbotMemberDoesNotCreatesAbbotMember()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var allMembers = await env.Db.Members.ToListAsync();
            env.Db.Members.RemoveRange(allMembers);
            await env.Db.SaveChangesAsync();
            var principal = new FakeClaimsPrincipal(
                platformId: organization.PlatformId,
                platformUserId: "U012345",
                platformTeamName: "The A Team",
                domain: "a.example.com");
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            var abbotMember = await env.Users.EnsureAbbotMemberAsync(organization);
            Assert.NotNull(abbotMember);
        }

        [Theory]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, true, true, false, false)]
        [InlineData(false, true, false, false, true)]
        [InlineData(false, true, true, false, false)]
        [InlineData(false, true, false, true, false)]
        [InlineData(false, false, false, false, true)]
        public async Task AddsRegistrationRequiredClaimUnderUnderSpecificCircumstances(
            bool botInstalled,
            bool adminsExist,
            bool isMember,
            bool requestedAccess,
            bool expectedRequiredClaim)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.AutoApproveUsers = false;
            var abbotMember = env.TestData.Abbot;
            var member = env.TestData.Member;
            var user = member.User;
            user.NameIdentifier = $"oauth2|slack|{user.PlatformUserId}";
            member.AccessRequestDate = requestedAccess
                ? DateTimeOffset.UtcNow
                : null;

            if (adminsExist)
            {
                var adminMember = await env.CreateAdminMemberAsync(org: organization);
                await env.Roles.AddUserToRoleAsync(
                    adminMember,
                    Roles.Administrator,
                    abbotMember);

                Assert.Equal(adminMember.OrganizationId, member.OrganizationId);
            }

            await env.Db.SaveChangesAsync();
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                nameIdentifier: user.NameIdentifier);

            if (isMember)
            {
                await env.Roles.AddUserToRoleAsync(member, Roles.Agent, abbotMember);
                principal.AddRoleClaim(Roles.Agent);
            }

            if (!botInstalled)
            {
                organization.PlatformBotId = null;
                organization.PlatformBotUserId = null;
                organization.ApiToken = null;
            }

            await env.Db.SaveChangesAsync();
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            Assert.NotNull(organization);
            Assert.NotNull(member);
            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.Equal(expectedRequiredClaim,
                principal.GetRegistrationStatusClaim() == RegistrationStatus.ApprovalRequired);
        }

        [Fact]
        public async Task AddsUserToAgentsDuringInstallation()
        {
            var env = TestEnvironment.CreateWithoutData();
            await env.Activate<RoleSeeder>().SeedDataAsync();
            await env.Db.SaveChangesAsync();
            env.BackgroundSlackClient.EnqueueMessageToInstallerCalled = false; // Need to reset this.
            var principal = new FakeClaimsPrincipal(
                "T00000001",
                "U000001",
                nameIdentifier: "oauth|slack|T00000001-U000001");
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            var organization = await env.Db.Organizations.SingleAsync();
            Assert.Equal("T00000001", organization.PlatformId);
            var members = await env.Db.Members.ToListAsync();
            Assert.Collection(members,
                m => Assert.True(m.IsAbbot()),
                m => Assert.Equal("U000001", m.User.PlatformUserId));
            var member = members.Last();
            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            Assert.False(env.BackgroundSlackClient.EnqueueMessageToInstallerCalled);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AddsUserToAgentsWhenAutoApproveUsersEnabledUnlessAdmin(bool isAdmin)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.PlatformBotId = "B01234";
            organization.AutoApproveUsers = true;
            await env.Db.SaveChangesAsync();
            var admin = await env.CreateAdminMemberAsync(); // Make sure the test user is not the first user.
            var member = env.TestData.Member;
            if (isAdmin)
            {
                await env.Roles.AddUserToRoleAsync(member, Roles.Administrator, admin);
            }
            var user = member.User;
            env.BackgroundSlackClient.EnqueueMessageToInstallerCalled = false; // Need to reset this.
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                nameIdentifier: user.NameIdentifier);

            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            await env.ReloadAsync(member);
            await env.ReloadAsync(member.MemberRoles.ToArray());

            if (isAdmin)
            {
                Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
                Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            }
            else
            {
                Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
                Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            }
            Assert.False(principal.GetRegistrationStatusClaim() is RegistrationStatus.ApprovalRequired);
            Assert.False(env.BackgroundSlackClient.EnqueueMessageToInstallerCalled);
        }

        [Fact]
        public async Task AddsUserToWaitListWhenAutoApproveUsersEnabledButOrganizationIsOutOfSeatsEvenWithInvitation()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.PlanType = PlanType.Business;
            organization.PurchasedSeatCount = 2;
            organization.PlatformBotId = "B01234";
            organization.AutoApproveUsers = true;
            var member = env.TestData.Member;
            member.InvitationDate = env.Clock.UtcNow.AddDays(-1);
            await env.Db.SaveChangesAsync();
            await env.CreateMemberInAgentRoleAsync();
            await env.CreateMemberInAgentRoleAsync();
            await env.CreateAdminMemberAsync(); // Make sure the test user is not the first user.
            var user = member.User;
            env.BackgroundSlackClient.EnqueueMessageToInstallerCalled = false; // Need to reset this.
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                nameIdentifier: user.NameIdentifier);
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            Assert.NotNull(organization);
            Assert.NotNull(member);
            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            Assert.True(principal.GetRegistrationStatusClaim() is RegistrationStatus.ApprovalRequired);
        }

        [Fact]
        public async Task AddsUserToAgentsWhenInvitedAndAutoApproveUsersNotEnabled()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.AutoApproveUsers = false;
            var member = env.TestData.Member;
            member.InvitationDate = env.Clock.UtcNow.AddDays(-1);
            await env.Db.SaveChangesAsync();
            var user = member.User;
            var abbotMember = await env.Organizations.EnsureAbbotMember(organization);
            var adminMember = await env.CreateMemberAsync();
            adminMember.User.NameIdentifier = $"oauth2|slack|{organization.PlatformId}";
            await env.Roles.AddUserToRoleAsync(adminMember, Roles.Administrator, abbotMember);
            env.BackgroundSlackClient.EnqueueMessageToInstallerCalled = false; // Need to reset this.
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                nameIdentifier: user.NameIdentifier);
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            Assert.DoesNotContain(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Agent);
            Assert.False(principal.GetRegistrationStatusClaim() is RegistrationStatus.ApprovalRequired);
            Assert.False(env.BackgroundSlackClient.EnqueueMessageToInstallerCalled);
        }

        [Fact]
        public async Task WhenBotInstalledAndNoAdministratorsCreatesUserAndAddToAdministratorRoleAndUpdatesOrganizationToAutoApproveUsers()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            await new RoleSeeder(env.Roles, env.Db).SeedDataAsync();
            var principal = new FakeClaimsPrincipal(organization.PlatformId, "U012345678");

            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleAuthenticatedUserAsync(principal);

            Assert.NotNull(organization);
            var member = await env.Db.Members
                .Include(m => m.MemberRoles)
                .ThenInclude(mr => mr.Role)
                .SingleAsync(m => m.OrganizationId == organization.Id && m.Active && !m.User.IsBot);

            Assert.Contains(member.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.False(principal.GetRegistrationStatusClaim() is RegistrationStatus.ApprovalRequired);
            Assert.True(env.BackgroundSlackClient.EnqueueMessageToInstallerCalled);
            Assert.True(organization.AutoApproveUsers);
        }
    }

    public class TheHandleValidatePrincipalAsyncMethod
    {
        [Fact]
        public async Task DoesNothingWithoutPrincipal()
        {
            var env = TestEnvironment.Create();

            var context = new FakeCookieValidatePrincipalContext { Principal = null };
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleValidatePrincipalAsync(context);

            Assert.False(context.ShouldRenew);
        }

        [Fact]
        public async Task DoesNothingForPrincipalWithoutPlatformTeamId()
        {
            var env = TestEnvironment.Create();

            var principal = new FakeClaimsPrincipal(platformId: null);
            var context = new FakeCookieValidatePrincipalContext(principal);
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleValidatePrincipalAsync(context);

            Assert.False(context.ShouldRenew);
        }

        [Theory]
        [InlineData("T_", null)]
        [InlineData(null, "U_")]
        public async Task RejectsPrincipalWithMissingMember(string? platformId, string? platformUserId)
        {
            var env = TestEnvironment.Create();

            var (user, org) = await env.CreateMemberAsync();
            var principal = new FakeClaimsPrincipal(platformId ?? org.PlatformId, platformUserId ?? user.PlatformUserId);
            var context = new FakeCookieValidatePrincipalContext(principal);
            Assert.NotNull(context.Principal);
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleValidatePrincipalAsync(context);

            Assert.False(context.ShouldRenew);
            Assert.Null(context.Principal);
        }

        [Fact]
        public async Task SetsCurrentMemberSyncsRolesAndSetsShouldRenew()
        {
            var env = TestEnvironment.Create();

            var member = await env.CreateAdminMemberAsync();
            var principal = new FakeClaimsPrincipal(member);
            Assert.Equal(new[] { Roles.Administrator }, principal.GetRoleClaimValues());

            await env.Roles.SyncRolesFromListAsync(member, new[] { Roles.Agent }, env.TestData.Abbot);

            var context = new FakeCookieValidatePrincipalContext(principal);
            var authenticationHandler = env.Activate<AuthenticationHandler>();

            await authenticationHandler.HandleValidatePrincipalAsync(context);

            var contextMember = context.HttpContext.GetCurrentMember();
            Assert.NotNull(contextMember);
            Assert.Equal(EntityState.Unchanged, env.Db.Entry(contextMember).State);
            Assert.Same(member, contextMember);

            Assert.Equal(new[] { Roles.Agent }, principal.GetRoleClaimValues());
            Assert.True(context.ShouldRenew);
        }
    }
}

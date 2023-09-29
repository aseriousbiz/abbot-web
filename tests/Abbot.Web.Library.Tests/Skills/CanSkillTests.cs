using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Skills;

public class CanSkillTests
{
    public class WhenCallerIsAdmin
    {
        [Theory]
        [InlineData(PlanType.Team, $"Permissions are a Business Plan feature. Contact us at `{WebConstants.SupportEmail}` to discuss upgrading your plan.", Capability.None)]
        [InlineData(PlanType.Business, "Ok, <@U0123456789> can use `target`.", Capability.Use)]
        [InlineData(PlanType.FoundingCustomer, "Ok, <@U0123456789> can use `target`.", Capability.Use)]
        public async Task WhenAddsPermissionToSkillWhenOnBusinessOrFoundingPlan(
            PlanType planType,
            string expectedReply,
            Capability expectedCapability)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var adminMember = await env.CreateAdminMemberAsync();
            var targetMember = env.TestData.Member;
            targetMember.User.PlatformUserId = "U0123456789";
            organization.PlanType = planType;
            await env.Db.SaveChangesAsync();
            var targetSkill = await env.CreateSkillAsync("target", restricted: true);
            var message = env.CreateFakeMessageContext(
                "can",
                "<@U0123456789> run target",
                adminMember,
                new[] { targetMember });
            var skill = env.Activate<CanSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(expectedReply, message.SingleReply());
            var capability = await env.Permissions.GetCapabilityAsync(targetMember, targetSkill);
            Assert.Equal(expectedCapability, capability);
        }

        [Fact]
        public async Task FailsWhenAddingPermissionForUnrecognizedUser()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var targetMember = env.TestData.Member;
            var adminMember = await env.CreateAdminMemberAsync();
            organization.PlanType = PlanType.Business;
            var targetSkill = await env.CreateSkillAsync("target", restricted: true);
            var message = env.CreateFakeMessageContext(
                "can",
                $"<@U09090909> run target",
                adminMember,
                new[] { targetMember });
            var skill = env.Activate<CanSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            var capability = await env.Permissions.GetCapabilityAsync(targetMember, targetSkill);
            Assert.Equal(Capability.None, capability);
            Assert.Equal("I did not recognize that user.", message.SingleReply());
        }

        [Fact]
        public async Task FailsWhenAddingPermissionForUserInAnotherOrg()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var anotherOrg = await env.CreateOrganizationAsync();
            var targetMember = await env.CreateMemberAsync(org: anotherOrg);
            var adminMember = await env.CreateAdminMemberAsync();
            organization.PlanType = PlanType.Business;
            var targetSkill = await env.CreateSkillAsync("target", restricted: true);
            var message = env.CreateFakeMessageContext(
                "can",
                $"<@{targetMember.User.PlatformUserId}> run target",
                adminMember,
                new[] { targetMember });
            var skill = env.Activate<CanSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            var capability = await env.Permissions.GetCapabilityAsync(targetMember, targetSkill);
            Assert.Equal(Capability.None, capability);
            Assert.Equal("I did not recognize that user.", message.SingleReply());
        }

        [Fact]
        public async Task CanRemovePermission()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var adminMember = await env.CreateAdminMemberAsync();
            var targetMember = env.TestData.Member;
            organization.PlanType = PlanType.Business;
            var targetSkill = await env.CreateSkillAsync("target", restricted: true);
            await env.Permissions.SetPermissionAsync(targetMember, targetSkill, Capability.Edit, adminMember);
            var message = env.CreateFakeMessageContext(
                "can",
                $"not <@{targetMember.User.PlatformUserId}> target",
                adminMember,
                new[] { targetMember });
            var skill = env.Activate<CanSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"Ok, <@{targetMember.User.PlatformUserId}> no longer has any permissions for `target` _(downgraded from `Edit`)_.",
                message.SingleReply());
            var capability = await env.Permissions.GetCapabilityAsync(targetMember, targetSkill);
            Assert.Equal(Capability.None, capability);
        }
    }

    public class WhenCallerIsNotAdmin
    {
        [Fact]
        public async Task ButHasAdminRightsToSkillCanAddPermissionForAnotherMember()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var delegateMember = await env.CreateMemberAsync();
            var targetMember = env.TestData.Member;
            targetMember.User.PlatformUserId = "U0123456789";
            organization.PlanType = PlanType.Business;
            var targetSkill = await env.CreateSkillAsync("target", restricted: true);
            await env.Permissions.SetPermissionAsync(delegateMember, targetSkill, Capability.Admin, env.TestData.Member);
            var message = env.CreateFakeMessageContext(
                "can",
                $"<@{targetMember.User.PlatformUserId}> run target",
                delegateMember,
                new[] { targetMember });
            var skill = env.Activate<CanSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal($"Ok, <@{targetMember.User.PlatformUserId}> can use `target`.", message.SingleReply());
            var capability = await env.Permissions.GetCapabilityAsync(targetMember, targetSkill);
            Assert.Equal(Capability.Use, capability);
        }

        [Theory]
        [InlineData(Capability.Admin, "Ok, <@U0123456789> can use `target`.", Capability.Use)]
        [InlineData(Capability.Edit, "I’m sorry, but you do not have permission to set permissions on this skill.", Capability.None)]
        public async Task ButDoesNotHaveAdminRightsToSkillCanNotAddPermissionForAnotherMember(
            Capability actorCapability,
            string expectedReply,
            Capability expectedCapability)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var delegateMember = await env.CreateMemberAsync();
            var targetMember = env.TestData.Member;
            targetMember.User.PlatformUserId = "U0123456789";
            organization.PlanType = PlanType.Business;
            var targetSkill = await env.CreateSkillAsync("target", restricted: true);
            await env.Permissions.SetPermissionAsync(delegateMember, targetSkill, actorCapability, env.TestData.Member);
            var message = env.CreateFakeMessageContext(
                "can",
                "<@U0123456789> run target",
                delegateMember,
                new[] { targetMember });
            var skill = env.Activate<CanSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(expectedReply, message.SingleReply());
            var capability = await env.Permissions.GetCapabilityAsync(targetMember, targetSkill);
            Assert.Equal(expectedCapability, capability);
        }
    }
}

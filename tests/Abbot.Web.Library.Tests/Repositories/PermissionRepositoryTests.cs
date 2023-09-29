using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

public class PermissionRepositoryTests
{
    public class TheGetCapabilityAsyncMethod
    {
        [Fact]
        public async Task ReturnsNoneWhenSkillDoesNotExist()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.GetCapabilityAsync(new Member(), new Skill { Id = 13 });

            Assert.Equal(Capability.None, result);
        }

        [Fact]
        public async Task ReturnsCapability()
        {
            var env = TestEnvironment.Create();
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = 42,
                SkillId = 23,
                Capability = Capability.Edit
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.GetCapabilityAsync(new Member { Id = 42 }, new Skill { Id = 23 });

            Assert.Equal(Capability.Edit, result);
        }

        [Fact]
        public async Task ReturnsFullControlIfUserIsInAdministratorRole()
        {
            var env = TestEnvironment.Create();
            var admin = await env.CreateAdminMemberAsync();
            var skill = await env.CreateSkillAsync("test");
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.GetCapabilityAsync(admin, skill);

            Assert.Equal(Capability.Admin, result);
        }

        [Fact]
        public async Task DowngradesCapabilityForForeignMember()
        {
            var env = TestEnvironment.Create();
            var admin = await env.CreateAdminMemberAsync();
            var skill = await env.CreateSkillAsync("test", org: env.TestData.ForeignOrganization);
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = admin.Id,
                SkillId = skill.Id,
                Capability = Capability.Admin
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.GetCapabilityAsync(admin, skill);

            Assert.Equal(Capability.Use, result);
        }
    }

    public class TheSetPermissionAsyncMethod
    {
        [Fact]
        public async Task CreatesNewPermission()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var admin = await env.CreateAdminMemberAsync();
            var skill = await env.CreateSkillAsync("test", restricted: true);
            var repository = env.Activate<PermissionRepository>();

            var previous = await repository.SetPermissionAsync(member, skill, Capability.Use, admin);

            var result = await repository.GetCapabilityAsync(member, skill);
            Assert.Equal(Capability.Use, result);
            Assert.Equal(Capability.None, previous);
        }

        [Fact]
        public async Task OverwritesPermission()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberAsync();
            var skill = await env.CreateSkillAsync("test");
            var repository = env.Activate<PermissionRepository>();
            await repository.SetPermissionAsync(member, skill, Capability.Admin, env.TestData.Member);

            var previous = await repository.SetPermissionAsync(member, skill, Capability.Use, env.TestData.Member);

            var result = await repository.GetCapabilityAsync(member, skill);
            Assert.Equal(Capability.Use, result);
            Assert.Equal(Capability.Admin, previous);
        }

        [Fact]
        public async Task WhenNoneRemovesPermission()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var skill = await env.CreateSkillAsync("test", restricted: true);
            var repository = env.Activate<PermissionRepository>();
            await repository.SetPermissionAsync(member, skill, Capability.Admin, member);

            var previous = await repository.SetPermissionAsync(member, skill, Capability.None, env.TestData.Member);

            var result = await repository.GetCapabilityAsync(member, skill);
            Assert.Equal(Capability.None, result);
            Assert.Equal(Capability.Admin, previous);
            Assert.Empty(await env.Db.Permissions.Where(p => p.SkillId == skill.Id).ToListAsync());
        }

        [Fact]
        public async Task ThrowsWhenActorNotInSameOrg()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var foreignAdmin = await env.CreateAdminMemberAsync(org: env.TestData.ForeignOrganization);
            var skill = await env.CreateSkillAsync("test", restricted: true);
            var repository = env.Activate<PermissionRepository>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SetPermissionAsync(member, skill, Capability.Use, foreignAdmin));
            Assert.Equal("Actor must be in the same organization as the skill.", ex.Message);
        }

        [Theory]
        [InlineData(Capability.Edit)]
        [InlineData(Capability.Admin)]
        public async Task ThrowsWhenAttemptingToAssignEditPermissionToMemberNotInSameOrg(Capability capability)
        {
            var env = TestEnvironment.Create();
            var foreignMember = env.TestData.ForeignMember;
            var admin = await env.CreateAdminMemberAsync();
            var skill = await env.CreateSkillAsync("test", restricted: true);
            var repository = env.Activate<PermissionRepository>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SetPermissionAsync(foreignMember, skill, capability, admin));
            Assert.Equal($"Cannot grant {capability} permissions to a member outside the skill's organization.", ex.Message);
        }
    }

    public class TheGetPermissionsForSkillAsyncMethod
    {
        [Fact]
        public async Task ReturnsCapability()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("pug");
            var memberOne = await env.CreateMemberInAgentRoleAsync();
            var memberTwo = await env.CreateMemberInAgentRoleAsync();
            await env.Db.Permissions.AddAsync(new Permission
            {
                Member = memberOne,
                Skill = skill,
                Capability = Capability.Edit
            });
            await env.Db.Permissions.AddAsync(new Permission
            {
                Member = memberTwo,
                Skill = skill,
                Capability = Capability.Admin
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.GetPermissionsForSkillAsync(skill);

            Assert.Equal(2, result.Count);
            Assert.Equal(Capability.Edit, result[0].Capability);
            Assert.Equal(Capability.Admin, result[1].Capability);
        }
    }
}

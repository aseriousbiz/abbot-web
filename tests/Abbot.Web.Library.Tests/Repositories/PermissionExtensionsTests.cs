using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;

public class PermissionExtensionsTests
{
    public class TheCanRunAsyncMethod
    {
        [Theory]
        [InlineData(Capability.None, false)]
        [InlineData(Capability.Use, true)]
        [InlineData(Capability.Edit, true)]
        [InlineData(Capability.Admin, true)]
        public async Task CanRunProtectedSkillIfCapabilityIsUseOrHigher(Capability capability, bool expected)
        {
            var env = TestEnvironment.Create();
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = 42,
                SkillId = 23,
                Capability = capability
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.CanRunAsync(
                new Member { Id = 42 },
                new Skill { Id = 23, Restricted = true });

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(Capability.None)]
        [InlineData(Capability.Use)]
        [InlineData(Capability.Edit)]
        [InlineData(Capability.Admin)]
        public async Task CanRunIfNotProtected(Capability capability)
        {
            var env = TestEnvironment.Create();
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = 42,
                SkillId = 23,
                Capability = capability
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.CanRunAsync(
                new Member { Id = 42 },
                new Skill { Id = 23, Restricted = false });

            Assert.True(result);
        }
    }

    public class TheCanEditAsyncMethod
    {
        [Theory]
        [InlineData(Capability.None, false)]
        [InlineData(Capability.Use, false)]
        [InlineData(Capability.Edit, true)]
        [InlineData(Capability.Admin, true)]
        public async Task CanEditIfCapabilityIsEditOrHigher(Capability capability, bool expected)
        {
            var env = TestEnvironment.Create();
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = 42,
                SkillId = 23,
                Capability = capability
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.CanEditAsync(
                new Member { Id = 42 },
                new Skill { Id = 23, Restricted = true });

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(Capability.None)]
        [InlineData(Capability.Use)]
        [InlineData(Capability.Edit)]
        [InlineData(Capability.Admin)]
        public async Task CanEditIfNotProtected(Capability capability)
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var skill = await env.CreateSkillAsync("test");
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = member.Id,
                SkillId = skill.Id,
                Capability = capability,
            });
            await env.Db.SaveChangesAsync();

            var repository = env.Activate<PermissionRepository>();

            var result = await repository.CanEditAsync(member, skill);
            Assert.True(result);
        }

        [Theory]
        [InlineData(Capability.None)]
        [InlineData(Capability.Use)]
        [InlineData(Capability.Edit)]
        [InlineData(Capability.Admin)]
        public async Task CanNotEditIfNotInSameOrg(Capability capability)
        {
            var env = TestEnvironment.Create();
            var foreignMember = env.TestData.ForeignMember;
            var skill = await env.CreateSkillAsync("test");
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = foreignMember.Id,
                SkillId = skill.Id,
                Capability = capability,
            });
            await env.Db.SaveChangesAsync();

            var repository = env.Activate<PermissionRepository>();

            var result = await repository.CanEditAsync(foreignMember, skill);

            Assert.False(result);
        }
    }

    public class TheCanAdministrateAsyncMethod
    {
        [Theory]
        [InlineData(true, Capability.None, false)]
        [InlineData(true, Capability.Use, false)]
        [InlineData(true, Capability.Edit, false)]
        [InlineData(true, Capability.Admin, true)]
        [InlineData(false, Capability.None, false)]
        [InlineData(false, Capability.Use, false)]
        [InlineData(false, Capability.Edit, false)]
        [InlineData(false, Capability.Admin, true)]
        public async Task CanAdministrateIfCapabilityIsAdminOrHigherNoMatterIfProtectedOrNot(
            bool isProtected,
            Capability capability,
            bool expected)
        {
            var env = TestEnvironment.Create();
            await env.Db.Permissions.AddAsync(new Permission
            {
                MemberId = 42,
                SkillId = 23,
                Capability = capability
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PermissionRepository>();

            var result = await repository.CanAdministrateAsync(
                new Member { Id = 42 },
                new Skill { Id = 23, Restricted = isProtected });

            Assert.Equal(expected, result);
        }
    }
}

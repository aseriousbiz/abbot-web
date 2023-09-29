using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Xunit;

public class AliasRepositoryTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesAnAlias()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var alias = new Alias
            {
                Name = "test-alias",
                Description = "A cool alias",
                TargetSkill = "echo",
                TargetArguments = "target args",
                Organization = organization
            };
            var repository = env.Activate<AliasRepository>();

            await repository.CreateAsync(alias, user);

            var result = await repository.GetAsync("test-alias", organization);
            Assert.NotNull(result);
            Assert.Equal(user, result.Creator);
            Assert.Equal("test-alias", result.Name);
            Assert.Equal("A cool alias", result.Description);
            Assert.Equal("echo", result.TargetSkill);
            Assert.Equal("target args", result.TargetArguments);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Created alias `test-alias` that targets skill `echo` with arguments `target args`.", log.Description);
        }

        [Fact]
        public async Task CreatesAnAliasWithNoArguments()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var alias = new Alias
            {
                Name = "test-alias",
                Description = "A cool alias",
                TargetSkill = "echo",
                Organization = organization
            };
            var repository = env.Activate<AliasRepository>();

            await repository.CreateAsync(alias, user);

            var result = await repository.GetAsync("test-alias", organization);
            Assert.NotNull(result);
            Assert.Equal(user, result.Creator);
            Assert.Equal("test-alias", result.Name);
            Assert.Equal("A cool alias", result.Description);
            Assert.Equal("echo", result.TargetSkill);
            Assert.Equal("", result.TargetArguments);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Created alias `test-alias` that targets skill `echo` with _no arguments_.", log.Description);
        }
    }

    public class TheRemoveAsyncMethod
    {
        [Fact]
        public async Task RemovesAnAlias()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var alias = new Alias
            {
                Name = "test-alias",
                Description = "A cool alias",
                TargetSkill = "echo",
                TargetArguments = "target args",
                Organization = organization
            };
            var repository = env.Activate<AliasRepository>();
            await repository.CreateAsync(alias, user);

            await repository.RemoveAsync(alias, user);

            var result = await repository.GetAsync("test-alias", organization);
            Assert.Null(result);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Removed alias `test-alias` that targets skill `echo` with arguments `target args`.", log.Description);
        }
    }

    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task IncludesCreator()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var alias = new Alias
            {
                Name = "test-alias",
                Description = "A cool alias",
                TargetSkill = "echo",
                TargetArguments = "target args",
                Organization = organization
            };
            var repository = env.Activate<AliasRepository>();
            await repository.CreateAsync(alias, user);

            var result = await repository.GetAllAsync(organization);

            Assert.NotNull(result[0].Creator);
        }
    }
}

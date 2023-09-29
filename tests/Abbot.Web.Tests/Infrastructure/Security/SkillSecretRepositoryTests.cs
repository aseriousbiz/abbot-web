using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Xunit;

public class SkillSecretRepositoryTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesSecretInDatabaseAndKeyVault()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var repository = env.Activate<SkillSecretRepository>();

            var secret = await repository.CreateAsync(
                "secret-key",
                "secret-value",
                "description",
                skill,
                user);

            var vaultSecret = await env.AzureKeyVaultClient.GetSecretAsync(secret.KeyVaultSecretName);
            Assert.NotNull(vaultSecret);
            Assert.Equal("test", vaultSecret.Properties.Tags["Skill"]);
            Assert.StartsWith(
                $"abbot-{skill.OrganizationId}-{skill.Id}-",
             vaultSecret.Name);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization)
                as SkillSecretEvent;
            Assert.NotNull(log);
            Assert.Equal("Created secret `secret-key` for skill `test`.", log.Description);
            Assert.Equal("description", log.SecretDescription);
            Assert.Equal("secret-key", log.SecretName);
            Assert.Equal(secret.KeyVaultSecretName, log.KeyVaultName);
            Assert.Equal(skill.Id, log.SkillId);
            Assert.Equal("test", log.SkillName);
        }
    }

    public class TheGetAsyncMethod
    {
        [Fact]
        public async Task GetsSecretBySkillNameAndSecretName()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillSecretRepository>();

            await repository.CreateAsync(
                "secret-key",
                "secret-value",
                "the description",
                skill,
                user);

            var secret = await repository.GetAsync("test-skill", "secret-key", organization);
            Assert.NotNull(secret);
            Assert.Equal("the description", secret.Description);
        }
    }

    public class TheGetSecretAsyncMethod
    {
        [Fact]
        public async Task GetsSecretBySecretNameSkillIdAndUserId()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillSecretRepository>();

            await repository.CreateAsync(
                "secret-key",
                "secret-value",
                "the description",
                skill,
                user);

            var secret = await repository.GetSecretAsync("secret-key", skill, user);
            Assert.NotNull(secret);
            Assert.Equal("secret-value", secret);
        }

        [Fact]
        public async Task CannotRetrieveSecretForAnotherOrganization()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillSecretRepository>();

            await repository.CreateAsync(
                "secret-key",
                "secret-value",
                "the description",
                skill,
                user);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.GetSecretAsync("secret-key", skill, env.TestData.ForeignUser));
        }
    }

    public class TheUpdateAsyncMethod
    {
        [Fact]
        public async Task UpdatesDescription()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillSecretRepository>();
            var skillSecret = await repository.CreateAsync(
                "secret-key",
                "secret-value",
                "the description",
                skill,
                user);

            await repository.UpdateAsync(
                skillSecret,
                null,
                "A new description",
                user);

            var secret = await repository.GetAsync("test-skill", "secret-key", organization);
            Assert.NotNull(secret);
            Assert.Equal("A new description", secret.Description);
        }
    }

    public class TheDeleteAsyncMethod
    {
        [Fact]
        public async Task DeletesSecretInDatabaseAndKeyVault()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var repository = env.Activate<SkillSecretRepository>();
            var secret = await repository.CreateAsync(
                "secret-key",
                "secret-value",
                "description",
                skill, user);

            await repository.DeleteAsync(secret, user);

            var vaultSecret = await env.AzureKeyVaultClient.GetSecretAsync(secret.KeyVaultSecretName);
            Assert.Null(vaultSecret);
            Assert.Empty(await env.Db.SkillSecrets.ToListAsync());
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Removed secret `secret-key` for skill `test`.", log.Description);
        }
    }
}

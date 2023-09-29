using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Repository for managing skill secrets. Includes support for creating the secret in
/// a key vault.
/// </summary>
public class SkillSecretRepository : ISkillSecretRepository
{
    readonly IAzureKeyVaultClient _keyVaultClient;
    readonly SkillSecretDatabase _repository;
    readonly AbbotContext _db;

    public SkillSecretRepository(AbbotContext db, IAzureKeyVaultClient keyVaultClient, IAuditLog auditLog)
    {
        _db = db;
        _keyVaultClient = keyVaultClient;
        _repository = new SkillSecretDatabase(db, auditLog);
    }

    public async Task<SkillSecret?> GetAsync(string skill, string name, Organization organization)
    {
        return await _db.SkillSecrets
            .Include(s => s.Skill)
            .FirstOrDefaultAsync(s => s.Skill.Name == skill
                                      && s.Name == name
                                      && s.OrganizationId == organization.Id);
    }

    public async Task<SkillSecret> CreateAsync(
        string name,
        string secretValue,
        string? description,
        Skill skill,
        User creator)
    {
        // The name must be a 1-127 character string, starting with a letter and containing only 0-9, a-z, A-Z, and -
        // https://docs.microsoft.com/en-us/azure/key-vault/general/about-keys-secrets-certificates
        var keyVaultName = $"abbot-{skill.OrganizationId}-{skill.Id}-{TokenCreator.CreateRandomString(24)}";
        var entity = new SkillSecret
        {
            Name = name.ToLowerInvariant(),
            Skill = skill,
            Description = description ?? string.Empty,
            Organization = skill.Organization,
            KeyVaultSecretName = keyVaultName
        };

        await _repository.CreateAsync(entity, creator);

        await CreateKeyVaultSecret(keyVaultName, secretValue, entity.Organization, entity.Skill, creator);
        return entity;
    }

    public async Task UpdateAsync(
        SkillSecret secret,
        string? newSecret,
        string? newDescription,
        User modifier)
    {
        if (newSecret is null && newDescription is null)
        {
            // Nothing to change.
            return;
        }

        if (newSecret is not null)
        {
            await CreateKeyVaultSecret(
                secret.KeyVaultSecretName,
                newSecret,
                secret.Organization,
                secret.Skill,
                modifier);
        }
        if (newDescription is not null)
        {
            secret.Description = newDescription;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(SkillSecret secret, User deleteBy)
    {
        await _keyVaultClient.DeleteSecretAsync(secret.KeyVaultSecretName);
        await _repository.RemoveAsync(secret, deleteBy);
    }

    Task CreateKeyVaultSecret(
        string name,
        string secretValue,
        Organization organization,
        Skill skill,
        User creator)
    {
        var secret = new KeyVaultSecret(name, secretValue);
        secret.Properties.Tags.Add("Organization", organization.Name);
        secret.Properties.Tags.Add("Skill", skill.Name);
        secret.Properties.Tags.Add("Creator", $"{creator.DisplayName} ({creator.Id})");
        return _keyVaultClient.CreateSecretAsync(secret);
    }

    public Task<IReadOnlyList<SkillSecret>> GetSecretsForSkillAsync(Skill skill)
    {
        return _repository.GetSecretsForSkillAsync(skill);
    }

    public async Task<string?> GetSecretAsync(string name, Id<Skill> skillId, Id<User> userId)
    {
        name = name.ToLowerInvariant();
        var entity = await _repository.GetAsync(name, skillId);
        if (entity is null)
        {
            return null;
        }

        // TODO: Log access
        var secretKey = entity.KeyVaultSecretName;
        var response = await _keyVaultClient.GetSecretAsync(secretKey);

        var member = await _db.Members
            .Include(m => m.User)
            .Include(m => m.Organization)
            .SingleOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == entity.OrganizationId);
        if (member is null)
        {
            throw new InvalidOperationException($"Non existent user {userId} tried to access secret.");
        }
        return response?.Value;
    }

    class SkillSecretDatabase : OrganizationScopedRepository<SkillSecret>
    {
        public SkillSecretDatabase(AbbotContext db, IAuditLog auditLog) : base(db, auditLog)
        {
        }

        public async Task<SkillSecret?> GetAsync(string name, Id<Skill> skillId)
        {
            name = name.ToLowerInvariant();
            return await Entities
                .Include(e => e.Skill)
                .SingleOrDefaultAsync(e => e.Name == name && e.SkillId == skillId.Value);
        }

        public Task<IReadOnlyList<SkillSecret>> GetSecretsForSkillAsync(Skill skill)
        {
            return GetAllAsync(skill.Organization, secret => secret.SkillId == skill.Id);
        }

        protected override DbSet<SkillSecret> Entities => Db.SkillSecrets;
    }
}

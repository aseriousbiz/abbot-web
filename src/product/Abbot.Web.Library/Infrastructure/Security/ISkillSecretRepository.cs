using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Repository for managing skill secrets. Includes support for creating the secret in
/// a key vault.
/// </summary>
public interface ISkillSecretRepository
{
    /// <summary>
    /// Retrieve a secret by skill name and secret name.
    /// </summary>
    /// <param name="skill">Name of the skill</param>
    /// <param name="name">Name of the skill key</param>
    /// <param name="organization">The organization the secret belongs to</param>
    Task<SkillSecret?> GetAsync(string skill, string name, Organization organization);

    /// <summary>
    /// Creates a secret with the specified values.
    /// </summary>
    /// <param name="name">The name of the secret</param>
    /// <param name="secretValue">The value of the secret</param>
    /// <param name="description">A description of the secret</param>
    /// <param name="skill">The <see cref="Skill"/> the secret belongs to</param>
    /// <param name="creator">The creator of the secret</param>
    Task<SkillSecret> CreateAsync(
        string name,
        string secretValue,
        string? description,
        Skill skill,
        User creator);

    /// <summary>
    /// Updates the specified secret.
    /// </summary>
    /// <param name="secret">The secret to update</param>
    /// <param name="newSecret">The new secret value, if changed</param>
    /// <param name="newDescription">The new description, if changed</param>
    /// <param name="modifier">The user making the change</param>
    Task UpdateAsync(
        SkillSecret secret,
        string? newSecret,
        string? newDescription,
        User modifier);

    /// <summary>
    /// Delete the specified secret.
    /// </summary>
    /// <param name="secret">The secret to delete</param>
    /// <param name="deleteBy">The user deleting the secret</param>
    Task DeleteAsync(SkillSecret secret, User deleteBy);

    /// <summary>
    /// Retrieves all the secrets for the specified skill.
    /// </summary>
    /// <param name="skill">The skill</param>
    Task<IReadOnlyList<SkillSecret>> GetSecretsForSkillAsync(Skill skill);

    /// <summary>
    /// Retrieves secret by name and skill Id.
    /// </summary>
    /// <remarks>
    /// This is used by the skill secret API and is designed this way to be more efficient.
    /// Rather than run a query to look up a skill and a user and pass them in, we pass in
    /// the IDs which have been encrypted in the header and verified by the controller.
    /// </remarks>
    /// <param name="name">Name of the secret.</param>
    /// <param name="skillId">The id of the skill the secret belongs to.</param>
    /// <param name="userId">The id of the user requesting the skill.</param>
    Task<string?> GetSecretAsync(string name, Id<Skill> skillId, Id<User> userId);
}

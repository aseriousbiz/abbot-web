using System.ComponentModel.DataAnnotations;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A reference to a secret a <see cref="Skill"/> may need to use.
/// The actual secret value is stored in Azure Key Vault.
/// </summary>
public class SkillSecret : TrackedEntityBase<SkillSecret>, IOrganizationEntity, INamedEntity, ISkillChildEntity, IAuditableEntity
{
    /// <summary>
    /// The name of the secret.
    /// </summary>
    [MaxLength(127)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The name of the secret entry in Azure Key Vault.
    /// </summary>
    [MaxLength(127)]
    public string KeyVaultSecretName { get; set; } = null!;

    /// <summary>
    /// A description of the secret such as what it is used for.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// The <see cref="Skill"/> this secret belongs to.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Skill"/> this secret belongs to.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// The Id of the <see cref="Organization"/> this secret belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> this secret belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        return new SkillSecretEvent
        {
            SecretId = Id,
            SkillId = Skill.Id,
            SkillName = Skill.Name,
            SecretName = Name,
            KeyVaultName = KeyVaultSecretName,
            SecretDescription = Description,
            Description = $"{auditOperation} secret `{Name}` for skill `{Skill.Name}`."
        };
    }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when creating or deleting a secret.
/// </summary>
public class SkillSecretEvent : SkillAuditEvent
{
    /// <summary>
    /// The Id of the secret.
    /// </summary>
    public int SecretId { get; set; }

    /// <summary>
    /// The name of the secret.
    /// </summary>
    public string SecretName { get; set; } = string.Empty;

    /// <summary>
    /// The Azure key vault name used to store this secret. Not exposed to customers.
    /// </summary>
    [Column("Code")]
    public string KeyVaultName { get; set; } = string.Empty;

    /// <summary>
    /// The description of the secret.
    /// </summary>
    [Column("ChangeDescription")]
    public string SecretDescription { get; set; } = string.Empty;

    [NotMapped]
    public override bool HasDetails => true;
}

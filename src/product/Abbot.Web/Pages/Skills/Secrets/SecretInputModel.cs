using System.ComponentModel.DataAnnotations;
using Serious.AspNetCore.DataAnnotations;

namespace Serious.Abbot.Pages.Skills.Secrets;

public class SecretInputModel
{
    [Display(Description = "Required. This is the name used to retrieve the secret in your skill code.")]
    [KeyVaultSecretName]
    public string Name { get; set; } = null!;

    [Display(Description = "Required. The secret value required by your skill.")]
    [DataType(DataType.Password)]
    public string Value { get; set; } = null!;

    [Display(Description = "Optional, but recommended. This will help you remember what the secret is for.")]
    public string? Description { get; set; }
}

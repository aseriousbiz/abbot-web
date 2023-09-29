using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Serious.AspNetCore.DataAnnotations;

/// <summary>
/// Validates an Azure Key Vault name such as the name of a secret.
/// </summary>
/// <remarks>
/// These names must be a 1-127 character string, containing only 0-9, a-z, A-Z, and -.
/// </remarks>
public sealed class KeyVaultSecretNameAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var text = value as string;

        return text switch
        {
            null => ValidationResult.Success,  // RequiredAttribute handles ensuring the value is specified.
            "" => ValidationResult.Success,    // RequiredAttribute handles ensuring the value is specified.
            _ when text.Length > 127 => GetErrorValidationResult(),
            _ when text.Any(c => !IsValidCharacter(c)) => GetErrorValidationResult(),
            _ => ValidationResult.Success
        };
    }

    static ValidationResult GetErrorValidationResult() =>
        new ValidationResult(
            "Name must be a 127 character or fewer string and may only contain 0-9, a-z, A-Z, and - characters.");

    static bool IsValidCharacter(char c)
    {
        return c.IsAsciiAlphaNumeric() || c == '-';
    }
}

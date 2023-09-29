using Azure.Security.KeyVault.Secrets;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Interface used to obtain a <see cref="SecretClient"/> used to to access secrets in Azure Key Vault.
/// </summary>
public interface IAzureKeyVaultSecretClientFactory
{
    SecretClient Create();
}

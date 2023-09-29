using System;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;

namespace Serious.Abbot.Infrastructure.Security;

// ReSharper disable once UnusedType.Global
// Type is used in non-Debug builds.
/// <summary>
/// Uses Azure Managed identity to access a <see cref="SecretClient"/> used to access secrets in Azure Key Vault.
/// </summary>
public class AzureKeyVaultSecretClientFactory : IAzureKeyVaultSecretClientFactory
{
    readonly IOptions<SkillOptions> _options;
    readonly IHostEnvironment _hostEnvironment;

    public AzureKeyVaultSecretClientFactory(IOptions<SkillOptions> options, IHostEnvironment hostEnvironment)
    {
        _options = options;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// Creates and returns a <see cref="SecretClient"/> used to access secrets in Azure Key Vault."/>
    /// </summary>
    /// <returns>A <see cref="SecretClient"/></returns>
    /// <exception cref="InvalidOperationException">Thrown if the key vault name is not configured.</exception>
    public SecretClient Create()
    {
        string keyVaultName = _options.Value.SecretVault?.Name
                              ?? throw new InvalidOperationException("Skill:SecretVault:Name not set.");

        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net");

        if (_options.Value.SecretVault.ClientId is { Length: > 0 } clientId)
        {
            var tenantId = _options.Value.SecretVault.TenantId
                           ?? throw new InvalidOperationException("Skill:SecretVault:TenantId is not set.");

            var clientSecret = _options.Value.SecretVault.ClientSecret
                               ?? throw new InvalidOperationException("Skill:SecretVault:ClientSecret is not set.");

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            return new SecretClient(keyVaultUri, credential);
        }

        return new SecretClient(keyVaultUri, new DefaultAzureCredential());
    }
}

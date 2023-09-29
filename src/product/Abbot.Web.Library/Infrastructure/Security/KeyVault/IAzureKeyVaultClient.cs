using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Interface that maps exactly to <see cref="SecretClient"/> for testability purposes.
/// </summary>
public interface IAzureKeyVaultClient
{
    /// <summary>
    /// Sets a secret in a specified key vault.
    /// </summary>
    /// <remarks>
    /// The set operation adds a secret to the Azure Key Vault. If the named secret
    /// already exists, Azure Key Vault creates a new version of that secret. This
    /// operation requires the secrets/set permission.
    /// </remarks>
    /// <param name="secret">The Secret object containing information about the secret and its properties. The properties secret.Name and secret.Value must be non null.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
    /// <exception cref="ArgumentNullException"><paramref name="secret"/> is null.</exception>
    /// <exception cref="RequestFailedException">The server returned an error. See <see cref="Exception.Message"/> for details returned from the server.</exception>
    Task<KeyVaultSecret> CreateSecretAsync(KeyVaultSecret secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specified secret from a given key vault.
    /// </summary>
    /// <remarks>
    /// The get operation is applicable to any secret stored in Azure Key Vault.
    /// This operation requires the secrets/get permission.
    /// </remarks>
    /// <param name="name">The name of the secret.</param>
    /// <param name="version">The version of the secret.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is an empty string.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="RequestFailedException">The server returned an error. See <see cref="Exception.Message"/> for details returned from the server.</exception>
    Task<KeyVaultSecret?> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret from a specified key vault.
    /// </summary>
    /// <remarks>
    /// The delete operation applies to any secret stored in Azure Key Vault.
    /// Delete cannot be applied to an individual version of a secret. This
    /// operation requires the secrets/delete permission.
    /// </remarks>
    /// <param name="name">The name of the secret.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
    /// <returns>
    /// A <see cref="DeleteSecretOperation"/> to wait on this long-running operation.
    /// If the Key Vault is soft delete-enabled, you only need to wait for the operation to complete if you need to recover or purge the secret;
    /// otherwise, the secret is deleted automatically on the <see cref="DeletedSecret.ScheduledPurgeDate"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is an empty string.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="RequestFailedException">The server returned an error. See <see cref="Exception.Message"/> for details returned from the server.</exception>
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default);
}

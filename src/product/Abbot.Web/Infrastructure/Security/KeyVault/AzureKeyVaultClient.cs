using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Wraps an instance of <see cref="SecretClient"/>.
/// </summary>
public class AzureKeyVaultClient : IAzureKeyVaultClient
{
    readonly SecretClient _client;

    public AzureKeyVaultClient(IAzureKeyVaultSecretClientFactory secretClientFactory)
    {
        _client = secretClientFactory.Create();
    }

    public async Task<KeyVaultSecret> CreateSecretAsync(KeyVaultSecret secret, CancellationToken cancellationToken = default)
    {
        var response = await _client.SetSecretAsync(secret, cancellationToken);
        if (response.Value is null)
        {
            var rawResponse = response.GetRawResponse();
            var responseBody = rawResponse.ContentStream is { } contentStream
                ? await new StreamReader(contentStream).ReadToEndAsync(cancellationToken)
                : null;
            throw new InvalidOperationException($"Could not create a secret with the name: {secret.Name}. The response was: {rawResponse.Status} {responseBody}");
        }
        return response.Value;
    }

    public async Task<KeyVaultSecret?> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetSecretAsync(name, version, cancellationToken);
        return response?.Value;
    }

    public Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        return _client.StartDeleteSecretAsync(name, cancellationToken);
    }
}

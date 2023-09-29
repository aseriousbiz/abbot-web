using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using Serious.Abbot.Infrastructure.Security;

namespace Serious.TestHelpers
{
    public class FakeAzureKeyVaultClient : IAzureKeyVaultClient
    {
        readonly Dictionary<string, KeyVaultSecret> _secrets = new();

        public Task<KeyVaultSecret> CreateSecretAsync(KeyVaultSecret secret, CancellationToken cancellationToken = default)
        {
            //TODO: See if we can simulate versioning.
            _secrets[secret.Name] = secret;
            return Task.FromResult(secret);
        }

        public Task<KeyVaultSecret?> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        {
            _secrets.TryGetValue(name, out var secret);
            return Task.FromResult(secret);
        }

        public Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            _secrets.Remove(name);
            return Task.CompletedTask;
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Storage.FileShare;

namespace Serious.TestHelpers
{
    public class FakeAssemblyCacheClient : IAssemblyCacheClient
    {
        readonly Dictionary<string, IAssemblyCacheDirectoryClient> _directoryClients = new Dictionary<string, IAssemblyCacheDirectoryClient>();

        public Task<IAssemblyCacheDirectoryClient> GetOrCreateAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
        {
            if (_directoryClients.TryGetValue(organizationIdentifier.ToCacheKey(), out var directoryClient))
            {
                return Task.FromResult(directoryClient);
            }
            IAssemblyCacheDirectoryClient client = new FakeAssemblyCacheDirectoryClient();
            _directoryClients.Add(organizationIdentifier.ToCacheKey(), client);
            return Task.FromResult(client);
        }

        public Task<IAssemblyCacheDirectoryClient?> GetAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
        {
            if (_directoryClients.TryGetValue(organizationIdentifier.ToCacheKey(), out var directoryClient))
            {
                return Task.FromResult<IAssemblyCacheDirectoryClient?>(directoryClient);
            }

            return Task.FromResult<IAssemblyCacheDirectoryClient?>(null);
        }
    }
}

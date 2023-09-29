using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Storage.FileShare;

namespace Serious.TestHelpers
{
    public class FakeAssemblyCacheDirectoryClient : IAssemblyCacheDirectoryClient
    {
        readonly Dictionary<string, IAssemblyClient> _assemblyClients = new Dictionary<string, IAssemblyClient>();

        public IAssemblyClient GetAssemblyClient(string cacheKey)
        {
            if (_assemblyClients.TryGetValue(cacheKey, out var assemblyClient))
            {
                return assemblyClient;
            }
            var client = new FakeAssemblyClient(
                new FakeShareFileClient(cacheKey),
                new FakeShareFileClient(
                $"{cacheKey}.pdb"));
            _assemblyClients.Add(cacheKey, client);
            return client;
        }

        public async IAsyncEnumerable<IAssemblyClient> GetCachedAssemblies()
        {
            foreach (var item in _assemblyClients.Values)
            {
                yield return await Task.FromResult(item);
            }
        }
    }
}

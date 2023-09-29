using System.Collections.Concurrent;
using System.Threading.Tasks;
using Serious.Abbot.Storage.FileShare;

namespace Serious.TestHelpers
{
    public class FakeShareClient : IShareClient
    {
        readonly ConcurrentDictionary<string, IShareDirectoryClient> _directories = new ConcurrentDictionary<string, IShareDirectoryClient>();

        bool Exists { get; set; }

        public Task<bool> ExistsAsync()
        {
            return Task.FromResult(Exists);
        }

        public Task CreateIfNotExistsAsync()
        {
            Exists = true;
            return Task.CompletedTask;
        }

        public IShareDirectoryClient GetDirectoryClient(string directoryName)
        {
            return _directories.GetOrAdd(directoryName, new FakeShareDirectoryClient(directoryName));
        }

        public IAssemblyCacheDirectoryClient CreateAssemblyCacheDirectoryClient(IShareDirectoryClient skillDirectoryClient)
        {
            return new FakeAssemblyCacheDirectoryClient();
        }
    }
}

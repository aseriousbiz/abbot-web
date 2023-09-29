using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Storage.FileShare;

namespace Serious.TestHelpers
{
    public class FakeShareDirectoryClient : IShareDirectoryClient
    {
        readonly ConcurrentDictionary<string, IShareDirectoryClient> _directories = new ConcurrentDictionary<string, IShareDirectoryClient>();
        readonly ConcurrentDictionary<string, IShareFileClient> _files = new ConcurrentDictionary<string, IShareFileClient>();

        public FakeShareDirectoryClient(string name)
        {
            Name = name;
        }

        public bool Exists { get; set; }

        public string Name { get; }

        public Task CreateIfNotExistsAsync()
        {
            Exists = true;
            return Task.CompletedTask;
        }

        public Task<IShareFileClient> CreateFileAsync(string fileName, long maxSize)
        {
            return Task.FromResult(_files.GetOrAdd(fileName, _ => new FakeShareFileClient(fileName) { Exists = true }));
        }

        public IShareFileClient GetFileClient(string fileName)
        {
            return _files.GetOrAdd(fileName, _ => new FakeShareFileClient(fileName));
        }

        public IShareDirectoryClient GetSubdirectoryClient(string subdirectoryName)
        {
            return _directories.GetOrAdd(subdirectoryName, _ => new FakeShareDirectoryClient(subdirectoryName));
        }

        public Task<bool> ExistsAsync()
        {
            return Task.FromResult(Exists);
        }

        public Task DeleteFileAsync(string fileName)
        {
            if (GetFileClient(fileName) is FakeShareFileClient fakeFile)
            {
                fakeFile.Exists = false;
                _files.Remove(fileName, out _);
            }

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<AzureShareFileItem> GetFilesAndDirectoriesAsync(string? prefix = default)
        {
            foreach (var file in _files.Values)
            {
                if (await file.ExistsAsync())
                {
                    yield return new AzureShareFileItem(false, file.Name);
                }
            }

            foreach (var directory in _directories.Values)
            {
                if (await directory.ExistsAsync())
                {
                    yield return new AzureShareFileItem(true, directory.Name);
                }
            }
        }

        public IAssemblyClient CreateAssemblyClient(IShareFileClient assemblyClient, IShareFileClient symbolsClient)
        {
            return new FakeAssemblyClient(assemblyClient, symbolsClient);
        }
    }
}

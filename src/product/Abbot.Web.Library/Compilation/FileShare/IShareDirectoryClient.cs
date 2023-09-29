using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serious.Abbot.Storage.FileShare;

public interface IShareDirectoryClient
{
    string Name { get; }
    Task CreateIfNotExistsAsync();
    IShareFileClient GetFileClient(string fileName);
    Task<bool> ExistsAsync();
    IAsyncEnumerable<AzureShareFileItem> GetFilesAndDirectoriesAsync(string? prefix = default);
    IAssemblyClient CreateAssemblyClient(IShareFileClient assemblyClient, IShareFileClient symbolsClient);
}

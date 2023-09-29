using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Logging;

namespace Serious.Abbot.Storage.FileShare;

public class AzureShareDirectoryClient : IShareDirectoryClient
{
    static readonly ILogger<AzureShareFileClient> Log = ApplicationLoggerFactory.CreateLogger<AzureShareFileClient>();

    readonly ShareDirectoryClient _directoryClient;

    public AzureShareDirectoryClient(ShareDirectoryClient directoryClient)
    {
        _directoryClient = directoryClient;
    }

    public string Name => _directoryClient.Name;

    public async Task CreateIfNotExistsAsync()
    {
        try
        {
            await _directoryClient.CreateIfNotExistsAsync();
        }
        catch (Exception e)
        {
            Log.ExceptionCreatingShare(e, _directoryClient.Name, "Share Directory");
            throw;
        }
    }

    public IShareFileClient GetFileClient(string fileName)
    {
        return new AzureShareFileClient(_directoryClient.GetFileClient(fileName));
    }

    public async Task<bool> ExistsAsync()
    {
        var response = await _directoryClient.ExistsAsync();
        return response.Value;
    }

    public async IAsyncEnumerable<AzureShareFileItem> GetFilesAndDirectoriesAsync(string? prefix = default)
    {
        await foreach (var info in _directoryClient.GetFilesAndDirectoriesAsync(prefix))
        {
            yield return new AzureShareFileItem(info);
        }
    }

    public IAssemblyClient CreateAssemblyClient(IShareFileClient assemblyClient, IShareFileClient symbolsClient)
    {
        return new AzureAssemblyClient(assemblyClient, symbolsClient);
    }
}

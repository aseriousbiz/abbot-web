using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Logging;

namespace Serious.Abbot.Storage.FileShare;

public class AzureShareFileClient : IShareFileClient
{
    static readonly ILogger<AzureShareFileClient> Log = ApplicationLoggerFactory.CreateLogger<AzureShareFileClient>();

    readonly ShareFileClient _fileClient;

    public AzureShareFileClient(ShareFileClient fileClient)
    {
        _fileClient = fileClient;
    }

    public string Name => _fileClient.Name;

    public async Task CreateAsync(long maxSize)
    {
        try
        {
            await _fileClient.CreateAsync(maxSize);
        }
        catch (Exception e)
        {
            Log.ExceptionCreatingShare(e, _fileClient.Name, "Share File");
            throw;
        }
    }

    public Task UploadRangeAsync(
        HttpRange range,
        Stream content)
    {
        return _fileClient.UploadRangeAsync(range, content);
    }

    public async Task<Stream> DownloadAsync()
    {
        var response = await _fileClient.DownloadAsync();
        return response.Value.Content;
    }

    public async Task<bool> ExistsAsync()
    {
        var response = await _fileClient.ExistsAsync();
        return response.Value;
    }

    public async Task<bool> DeleteIfExistsAsync()
    {
        var response = await _fileClient.DeleteIfExistsAsync();
        return response.Value; // True if the file existed
    }

    public async Task<IDictionary<string, string>> GetMetadataAsync()
    {
        var response = await _fileClient.GetPropertiesAsync(CancellationToken.None);
        return response.Value.Metadata;
    }

    public async Task SetMetadataAsync(IDictionary<string, string> metadata)
    {
        await _fileClient.SetMetadataAsync(metadata, CancellationToken.None);
    }
}

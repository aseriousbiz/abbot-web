using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Azure;

namespace Serious.Abbot.Storage.FileShare;

public class AzureAssemblyClient : IAssemblyClient
{
    readonly IShareFileClient _assemblyFileClient;
    readonly IShareFileClient _assemblySymbolsFileClient;

    public AzureAssemblyClient(IShareFileClient assemblyFileClient, IShareFileClient assemblySymbolsFileClient)
    {
        Name = assemblyFileClient.Name;
        _assemblyFileClient = assemblyFileClient;
        _assemblySymbolsFileClient = assemblySymbolsFileClient;
    }

    public string Name { get; }

    public Task<bool> ExistsAsync()
    {
        return _assemblyFileClient.ExistsAsync();
    }

    public Task<bool> SymbolsExistAsync()
    {
        return _assemblySymbolsFileClient.ExistsAsync();
    }

    public async Task<Stream> DownloadAssemblyAsync()
    {
        await SetDateLastAccessedAsync(DateTimeOffset.UtcNow);
        return await _assemblyFileClient.DownloadAsync();
    }

    public Task<Stream> DownloadSymbolsAsync()
    {
        return _assemblySymbolsFileClient.DownloadAsync();
    }

    public async Task UploadAsync(Stream assemblyStream, Stream assemblySymbolsStream)
    {
        await Task.WhenAll(
            _assemblyFileClient.CreateAsync(assemblyStream.Length),
            assemblySymbolsStream.Length > 0 ? _assemblySymbolsFileClient.CreateAsync(assemblySymbolsStream.Length) : Task.CompletedTask);

        await Task.WhenAll(
            _assemblyFileClient.UploadRangeAsync(
                new HttpRange(0, assemblyStream.Length),
                assemblyStream),
            assemblySymbolsStream.Length > 0
                ? _assemblySymbolsFileClient.UploadRangeAsync(new HttpRange(0, assemblySymbolsStream.Length), assemblySymbolsStream)
                : Task.CompletedTask);
        await SetDateLastAccessedAsync(DateTimeOffset.UtcNow);
    }

    public async Task DeleteIfExistsAsync()
    {
        await Task.WhenAll(
            _assemblyFileClient.DeleteIfExistsAsync(),
            _assemblySymbolsFileClient.DeleteIfExistsAsync());
    }

    const string DateLastAccessed = nameof(DateLastAccessed);

    public async Task<DateTimeOffset> GetDateLastAccessedAsync()
    {
        var metadata = await _assemblyFileClient.GetMetadataAsync();
        return metadata.TryGetValue(DateLastAccessed, out var lastAccessedDate)
            ? DateTimeOffset.TryParse(
                lastAccessedDate,
                null,
                DateTimeStyles.RoundtripKind, out var parsedDateTime)
                ? parsedDateTime
                : DateTimeOffset.MinValue
            : DateTimeOffset.MinValue;
    }

    public async Task SetDateLastAccessedAsync(DateTimeOffset dateTimeOffset)
    {
        await _assemblyFileClient.SetMetadataAsync(new Dictionary<string, string>
        {
            { DateLastAccessed, dateTimeOffset.ToString("o") }
        });
    }
}

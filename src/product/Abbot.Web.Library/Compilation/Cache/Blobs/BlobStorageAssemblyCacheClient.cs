using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Serious.Abbot.Messages;
using Serious.Abbot.Storage.FileShare;

namespace Serious.Abbot.Compilation.Cache.Blobs;

/// <summary>
/// Stores assembly caches in Azure Blob Storage.
/// </summary>
public class BlobStorageAssemblyCacheClient : IAssemblyCacheClient
{
    readonly IOptions<CompilationOptions> _options;
    readonly BlobServiceClient _client;
    readonly string _containerName;

    public BlobStorageAssemblyCacheClient(IOptions<CompilationOptions> options)
    {
        _options = options;

        if (_options.Value.AccountName is { Length: > 0 } accountName)
        {
            var blobUri = new Uri($"https://{accountName}.blob.core.windows.net/");
            _client = new BlobServiceClient(blobUri, new DefaultAzureCredential());
        }
        else if (_options.Value.ConnectionString is { Length: > 0 } connectionString)
        {
            _client = new BlobServiceClient(connectionString);
        }
        else
        {
            throw new InvalidOperationException(
                "One of Compilation:AccountName or Compilation:ConnectionString must be set when Compilation:Provider is 'blobs'");
        }

        _containerName = _options.Value.ContainerName ?? throw new InvalidOperationException("Compilation:ContainerName must be set when Compilation:Provider is 'blobs'");
    }

    public async Task<IAssemblyCacheDirectoryClient> GetOrCreateAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
    {
        var container = _client.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync();
        var directory = GetPlatformDirectoryName(organizationIdentifier);
        return new BlobCacheDirectory(container, directory);
    }

    public async Task<IAssemblyCacheDirectoryClient?> GetAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
    {
        // There's no concept of a "missing" cache directory in blob storage since there are no directories.
        return await GetOrCreateAssemblyCacheAsync(organizationIdentifier);
    }

    static string GetPlatformDirectoryName(IOrganizationIdentifier organizationIdentifier)
    {
        return $"{organizationIdentifier.PlatformType}-{organizationIdentifier.PlatformId}".ToLowerInvariant();
    }
}

public class BlobCacheDirectory : IAssemblyCacheDirectoryClient
{
    readonly BlobContainerClient _container;
    readonly string _directory;

    public BlobCacheDirectory(BlobContainerClient container, string directory)
    {
        _container = container;
        _directory = directory;
    }

    public IAssemblyClient GetAssemblyClient(string cacheKey)
    {
        return new BlobAssemblyClient(_container, _directory, cacheKey);
    }

    public async IAsyncEnumerable<IAssemblyClient> GetCachedAssemblies()
    {
        // We're taking a bit of a risk here by storing the entire container contents.
        await foreach (var blob in _container.GetBlobsByHierarchyAsync(prefix: $"{_directory}/", delimiter: "/"))
        {
            if (blob.IsPrefix)
            {
                var name = blob.Prefix[(_directory.Length + 1)..^1];
                yield return new BlobAssemblyClient(_container, _directory, name);
            }
        }
    }
}

public class BlobAssemblyClient : IAssemblyClient
{
    readonly BlobContainerClient _container;
    readonly string _directory;

    public BlobAssemblyClient(BlobContainerClient container, string directory, string cacheKey)
    {
        Name = cacheKey;
        _container = container;
        _directory = directory;
    }

    public string Name { get; }

    public async Task<bool> ExistsAsync()
    {
        return await _container.GetBlobClient(GetFilePath("dll")).ExistsAsync();
    }

    public async Task<bool> SymbolsExistAsync()
    {
        return await _container.GetBlobClient(GetFilePath("pdb")).ExistsAsync();
    }

    public async Task<Stream> DownloadAssemblyAsync() =>
        await DownloadFileAsync("dll");

    public async Task<Stream> DownloadSymbolsAsync() =>
        await DownloadFileAsync("pdb");

    public async Task UploadAsync(Stream assemblyStream, Stream assemblySymbolsStream)
    {
        await _container.GetBlobClient(GetFilePath("dll")).UploadAsync(assemblyStream);
        await _container.GetBlobClient(GetFilePath("pdb")).UploadAsync(assemblySymbolsStream);
    }

    public async Task DeleteIfExistsAsync()
    {
        await _container.GetBlobClient(GetFilePath("dll")).DeleteIfExistsAsync();
        await _container.GetBlobClient(GetFilePath("pdb")).DeleteIfExistsAsync();
    }

    public Task<DateTimeOffset> GetDateLastAccessedAsync()
    {
        // This is only used to delay purging the cache,
        // but we already avoid purging an assembly if it's associated with an existing skill.
        return Task.FromResult(DateTimeOffset.MinValue);
    }

    public Task SetDateLastAccessedAsync(DateTimeOffset dateTimeOffset)
    {
        // Let Azure handle the last accessed date.
        // Worst case we purge assemblies too often and have to recompile them 🤷.
        return Task.CompletedTask;
    }

    async Task<Stream> DownloadFileAsync(string extension)
    {
        var path = GetFilePath(extension);
        var result = await _container.GetBlobClient(path).DownloadAsync();
        if (result is { Value: { } v })
        {
            return v.Content;
        }

        throw new FileNotFoundException($"Failed to download from {path}");
    }

    string GetFilePath(string extension)
    {
        return $"{_directory}/{Name}/{Name}.{extension}";
    }
}

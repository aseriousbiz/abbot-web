using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Configuration;
using Serious.Abbot.Messages;
using Serious.Abbot.Storage.FileShare;

namespace Serious.Abbot.Compilation.Cache.Local;

public class LocalAssemblyCacheClient : IAssemblyCacheClient
{
    readonly string _rootPath;

    public LocalAssemblyCacheClient()
    {
        _rootPath = DevelopmentEnvironment.GetLocalStorePath("assemblyCache");
    }

    public Task<IAssemblyCacheDirectoryClient> GetOrCreateAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
    {
        var cachePath = Path.Combine(_rootPath, GetPlatformDirectoryName(organizationIdentifier));
        Directory.CreateDirectory(cachePath); // Does not throw if directories already exist.
        return Task.FromResult<IAssemblyCacheDirectoryClient>(new LocalAssemblyCacheDirectory(cachePath));
    }

    public Task<IAssemblyCacheDirectoryClient?> GetAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
    {
        var cachePath = Path.Combine(_rootPath, GetPlatformDirectoryName(organizationIdentifier));
        if (Directory.Exists(cachePath))
        {
            return Task.FromResult<IAssemblyCacheDirectoryClient?>(new LocalAssemblyCacheDirectory(cachePath));
        }
        else
        {
            return Task.FromResult<IAssemblyCacheDirectoryClient?>(null);
        }
    }

    static string GetPlatformDirectoryName(IOrganizationIdentifier organizationIdentifier)
    {
        return $"{organizationIdentifier.PlatformType}-{organizationIdentifier.PlatformId}";
    }
}

public class LocalAssemblyCacheDirectory : IAssemblyCacheDirectoryClient
{
    readonly string _cachePath;

    public LocalAssemblyCacheDirectory(string cachePath)
    {
        _cachePath = cachePath;
    }

    public IAssemblyClient GetAssemblyClient(string cacheKey)
    {
        var assemblyDir = Path.Combine(_cachePath, cacheKey);
        return new LocalAssemblyClient(cacheKey, assemblyDir);
    }

    public async IAsyncEnumerable<IAssemblyClient> GetCachedAssemblies()
    {
        var dirs = Directory.GetDirectories(_cachePath);
        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            yield return new LocalAssemblyClient(name, dir);
        }
    }
}

public class LocalAssemblyClient : IAssemblyClient
{
    readonly string _dirPath;
    readonly string _dllPath;
    readonly string _pdbPath;

    public LocalAssemblyClient(string name, string dirPath)
    {
        _dirPath = dirPath;
        Name = name;
        _dllPath = Path.Combine(dirPath, $"{name}.dll");
        _pdbPath = Path.Combine(dirPath, $"{name}.pdb");
    }

    public string Name { get; }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(File.Exists(_dllPath));
    }

    public Task<bool> SymbolsExistAsync()
    {
        return Task.FromResult(File.Exists(_pdbPath));
    }

    public Task<Stream> DownloadAssemblyAsync()
    {
        return Task.FromResult<Stream>(File.OpenRead(_dllPath));
    }

    public Task<Stream> DownloadSymbolsAsync()
    {
        return Task.FromResult<Stream>(File.OpenRead(_pdbPath));
    }

    public async Task UploadAsync(Stream assemblyStream, Stream assemblySymbolsStream)
    {
        Directory.CreateDirectory(_dirPath);
        await using var dllStream = new FileStream(_dllPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await assemblyStream.CopyToAsync(dllStream);
        await using var pdbStream = new FileStream(_pdbPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await assemblySymbolsStream.CopyToAsync(pdbStream);
    }

    public Task DeleteIfExistsAsync()
    {
        // > If the file to be deleted does not exist, no exception is thrown.
        // https://docs.microsoft.com/en-us/dotnet/api/system.io.file.delete?view=net-6.0#remarks
        File.Delete(_dllPath);
        File.Delete(_pdbPath);
        Directory.Delete(_dirPath);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset> GetDateLastAccessedAsync()
    {
        return Task.FromResult(new DateTimeOffset(File.GetLastAccessTimeUtc(_dllPath), TimeSpan.Zero));
    }

    public Task SetDateLastAccessedAsync(DateTimeOffset dateTimeOffset)
    {
        File.SetLastAccessTimeUtc(_dllPath, dateTimeOffset.UtcDateTime);
        File.SetLastAccessTimeUtc(_pdbPath, dateTimeOffset.UtcDateTime);
        return Task.CompletedTask;
    }
}

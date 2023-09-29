using System;
using System.IO;
using System.Threading.Tasks;

namespace Serious.Abbot.Storage.FileShare;

/// <summary>
/// A client to an assembly stored in Azure FileShare.
/// </summary>
public interface IAssemblyClient
{
    /// <summary>
    /// The name of the assembly file. This is the same thing as the cache key.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether or not the assembly file exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Whether or not the symbols exists for the assembly file.
    /// </summary>
    Task<bool> SymbolsExistAsync();

    /// <summary>
    /// Download the assembly file as a stream.
    /// </summary>
    Task<Stream> DownloadAssemblyAsync();

    /// <summary>
    /// Download the assembly symbols as a stream.
    /// </summary>
    Task<Stream> DownloadSymbolsAsync();

    /// <summary>
    /// Upload the assembly and optionally its symbols to the file share.
    /// </summary>
    /// <param name="assemblyStream">The assembly as a stream</param>
    /// <param name="assemblySymbolsStream">The assembly symbols as a stream</param>
    Task UploadAsync(Stream assemblyStream, Stream assemblySymbolsStream);

    /// <summary>
    /// Deletes the assembly file and its symbols file if they exist.
    /// </summary>
    Task DeleteIfExistsAsync();

    /// <summary>
    /// Returns the date the assembly was last read.
    /// </summary>
    Task<DateTimeOffset> GetDateLastAccessedAsync();

    /// <summary>
    /// Returns the date the assembly was last read.
    /// </summary>
    Task SetDateLastAccessedAsync(DateTimeOffset dateTimeOffset);
}

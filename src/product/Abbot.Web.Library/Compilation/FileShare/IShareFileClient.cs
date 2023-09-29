using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;

namespace Serious.Abbot.Storage.FileShare;

public interface IShareFileClient
{
    /// <summary>
    /// The file name for the share file.
    /// </summary>
    string Name { get; }

    Task CreateAsync(long maxSize);

    Task UploadRangeAsync(
        HttpRange range,
        Stream content);

    Task<Stream> DownloadAsync();

    Task<bool> ExistsAsync();

    Task<bool> DeleteIfExistsAsync();

    /// <summary>
    /// Retrieves metadata we set for the share file.
    /// </summary>
    Task<IDictionary<string, string>> GetMetadataAsync();

    /// <summary>
    /// Sets some metadata on the share file. We can use this to track when the file was last read.
    /// </summary>
    /// <param name="metadata">A dictionary of metadata keys and values</param>
    Task SetMetadataAsync(IDictionary<string, string> metadata);
}

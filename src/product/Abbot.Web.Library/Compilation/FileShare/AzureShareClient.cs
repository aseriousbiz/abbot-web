using System;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Logging;

namespace Serious.Abbot.Storage.FileShare;

/// <summary>
/// The <see cref="AzureShareClient"/> wraps a <see cref="ShareClient"/> which allows you to manipulate Azure
/// Storage shares and their directories and files.
/// </summary>
public class AzureShareClient : IShareClient
{
    static readonly ILogger<AzureShareClient> Log = ApplicationLoggerFactory.CreateLogger<AzureShareClient>();

    readonly ShareClient _shareClient;

    public AzureShareClient(ShareClient shareClient)
    {
        _shareClient = shareClient;
    }

    /// <summary>
    /// The <see cref="ExistsAsync"/> operation can be called on a
    /// <see cref="AzureShareClient"/> to see if the associated share
    /// exists on the storage account in the storage service.
    /// </summary>
    /// <returns>
    /// Returns true if the share exists.
    /// </returns>
    /// <remarks>
    /// A <see cref="RequestFailedException"/> will be thrown if
    /// a failure occurs.
    /// </remarks>
    public async Task<bool> ExistsAsync()
    {
        var response = await _shareClient.ExistsAsync();
        return response.Value;
    }

    /// <summary>
    /// <para>
    /// Creates a new share under the specified account. If a share with the same name
    /// already exists, it is not changed.
    /// </para>
    /// <para>
    /// For more information, see
    /// <see href="https://docs.microsoft.com/rest/api/storageservices/create-share">Create Share</see>.
    /// </para>
    /// </summary>
    /// <returns>
    /// A <see cref="Response{ShareInfo}"/> describing the newly
    /// created share.  If the share already exists, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// A <see cref="RequestFailedException"/> will be thrown if
    /// a failure occurs.
    /// </remarks>
    public async Task CreateIfNotExistsAsync()
    {
        try
        {
            await _shareClient.CreateIfNotExistsAsync();
        }
        catch (Exception e)
        {
            Log.ExceptionCreatingShare(e, _shareClient.Name, "Share");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a <see cref="IShareDirectoryClient" /> for the specified <paramref name="directoryName"/>.
    /// </summary>
    /// <param name="directoryName">The name of the top-level directory to retrieve. It should not be a path.</param>
    /// <returns>A <see cref="IShareDirectoryClient"/> used to manipulate the directory.</returns>
    public IShareDirectoryClient GetDirectoryClient(string directoryName)
    {
        return new AzureShareDirectoryClient(_shareClient.GetDirectoryClient(directoryName));
    }

    /// <summary>
    /// Creates and returns an <see cref="IAssemblyCacheDirectoryClient" /> in the specified directory.
    /// </summary>
    /// <remarks>
    /// This creates a directory in the share for storing assemblies.
    /// </remarks>
    /// <param name="skillDirectoryClient">The <see cref="IShareDirectoryClient"/> for the directory in which to create the assembly cache directory.</param>
    /// <returns>An <see cref="IAssemblyCacheDirectoryClient"/> used to store assemblies.</returns>
    public IAssemblyCacheDirectoryClient CreateAssemblyCacheDirectoryClient(IShareDirectoryClient skillDirectoryClient)
    {
        return new AssemblyCacheDirectoryClient(skillDirectoryClient);
    }
}

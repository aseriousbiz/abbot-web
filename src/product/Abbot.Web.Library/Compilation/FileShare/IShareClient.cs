using System.Threading.Tasks;
using Azure;
using Azure.Storage.Files.Shares;

namespace Serious.Abbot.Storage.FileShare;

/// <summary>
/// The <see cref="IShareClient"/> interface is an abstraction for
/// <see cref="ShareClient"/> which allows you to manipulate Azure
/// Storage shares and their directories and files.
/// </summary>
public interface IShareClient
{
    /// <summary>
    /// Checks to see if the associated share exists on the storage account in the storage service.
    /// </summary>
    /// <returns>
    /// Returns true if the share exists.
    /// </returns>
    /// <remarks>
    /// A <see cref="RequestFailedException"/> will be thrown if
    /// a failure occurs.
    /// </remarks>
    Task<bool> ExistsAsync();

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
    Task CreateIfNotExistsAsync();

    /// <summary>
    /// Retrieves a <see cref="IShareDirectoryClient" /> for the specified <paramref name="directoryName"/>.
    /// </summary>
    /// <param name="directoryName">The name of the top-level directory to retrieve. It should not be a path.</param>
    /// <returns>A <see cref="IShareDirectoryClient"/> used to manipulate the directory.</returns>
    IShareDirectoryClient GetDirectoryClient(string directoryName);

    /// <summary>
    /// Creates and returns an <see cref="IAssemblyCacheDirectoryClient" /> in the specified directory.
    /// </summary>
    /// <remarks>
    /// This creates a directory in the share for storing assemblies.
    /// </remarks>
    /// <param name="skillDirectoryClient">The <see cref="IShareDirectoryClient"/> for the directory in which to create the assembly cache directory.</param>
    /// <returns>An <see cref="IAssemblyCacheDirectoryClient"/> used to store assemblies.</returns>
    IAssemblyCacheDirectoryClient CreateAssemblyCacheDirectoryClient(IShareDirectoryClient skillDirectoryClient);
}

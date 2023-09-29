using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Storage.FileShare;

/// <summary>
/// The client to Azure File Share that is the starting point to accessing
/// compiled skill assemblies for an organization's chat platform.
/// </summary>
public interface IAssemblyCacheClient
{
    /// <summary>
    /// Gets or creates the assembly cache for the specified organization.
    /// </summary>
    /// <param name="organizationIdentifier">Uniquely identifies the chat platform team or org.</param>
    Task<IAssemblyCacheDirectoryClient> GetOrCreateAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier);

    /// <summary>
    /// Retrieve the client for the assembly cache directory for the specified chat platform, returning <c>null</c> if the directory does not exist.
    /// </summary>
    /// <param name="organizationIdentifier">Uniquely identifies the chat platform team or org.</param>
    Task<IAssemblyCacheDirectoryClient?> GetAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier);
}

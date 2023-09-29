using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Storage.FileShare;

/// <summary>
/// The client to Azure File Share that is the starting point to accessing
/// compiled skill assemblies for an organization's chat platform.
/// </summary>
public class AzureAssemblyCacheClient : IAssemblyCacheClient
{
    readonly IShareClient _azureShareClient;

    public AzureAssemblyCacheClient(IShareClient azureShareClient)
    {
        _azureShareClient = azureShareClient;
    }

    /// <summary>
    /// Retrieve the client for the assembly cache directory for the specified chat platform.
    /// </summary>
    /// <param name="organizationIdentifier">Uniquely identifies the chat platform team or org.</param>
    /// <remarks>
    /// This will return an Azure File Share directory client named {PlatformType}-{PlatformId}. For example,
    /// "Slack-T0123456789".
    /// </remarks>
    /// <returns>
    /// An <see cref="IAssemblyCacheDirectoryClient" /> for the directory containing all the
    /// compiled skills for this organizations' chat platform.
    /// </returns>
    public async Task<IAssemblyCacheDirectoryClient> GetOrCreateAssemblyCacheAsync(
        IOrganizationIdentifier organizationIdentifier)
    {
        await _azureShareClient.CreateIfNotExistsAsync();
        var platformDirectory = _azureShareClient.GetDirectoryClient(GetPlatformDirectoryName(organizationIdentifier));
        await platformDirectory.CreateIfNotExistsAsync();
        return _azureShareClient.CreateAssemblyCacheDirectoryClient(platformDirectory);
    }

    public async Task<IAssemblyCacheDirectoryClient?> GetAssemblyCacheAsync(IOrganizationIdentifier organizationIdentifier)
    {
        if (!await _azureShareClient.ExistsAsync())
        {
            return null;
        }
        var platformDirectory = _azureShareClient.GetDirectoryClient(GetPlatformDirectoryName(organizationIdentifier));
        if (!await platformDirectory.ExistsAsync())
        {
            return null;
        }
        return _azureShareClient.CreateAssemblyCacheDirectoryClient(platformDirectory);
    }

    static string GetPlatformDirectoryName(IOrganizationIdentifier organizationIdentifier)
    {
        return $"{organizationIdentifier.PlatformType}-{organizationIdentifier.PlatformId}";
    }
}

using System.IO;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Infrastructure;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Clients;

public class WebStorageOptions
{
    /// <summary>
    /// The blob endpoint of the storage account to use.
    /// If this is set, it is used instead of <see cref="ConnectionString"/> and assumes that managed identity is configured.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// A connection string to a storage account to use.
    /// This is ignored if <see cref="AccountName"/> is set.
    /// </summary>
    public string? ConnectionString { get; set; }
}

/// <summary>
/// Client used to store customer files such as profile images and uploaded images.
/// </summary>
public class AbbotWebFileStorage : IAbbotWebFileStorage
{
    readonly IOptions<WebStorageOptions> _options;
    readonly IConfiguration _configuration;
    static readonly ILogger<AbbotWebFileStorage> Log =
        ApplicationLoggerFactory.CreateLogger<AbbotWebFileStorage>();

    public AbbotWebFileStorage(IOptions<WebStorageOptions> options, IConfiguration configuration)
    {
        _options = options;
        _configuration = configuration;
    }

    /// <summary>
    /// Uploads an organization avatar and returns the URL to the image.
    /// </summary>
    /// <param name="platformId">The platform id for the organization.</param>
    /// <param name="imageStream">The stream containing the image.</param>
    /// <returns>A Task with the <see cref="Uri"/> of the uploaded image.</returns>
    public async Task<Uri> UploadOrganizationAvatarAsync(string platformId, Stream imageStream)
    {
        return await UploadImageAsync("bots", platformId, imageStream);
    }

    async Task<Uri> UploadImageAsync(
        string clientName,
        string folder,
        Stream imageStream)
    {
        var blobServiceClient = GetBlobServiceClient();
        var blobContainer = blobServiceClient.GetBlobContainerClient(clientName);
        await blobContainer.CreateIfNotExistsAsync(PublicAccessType.Blob);
        string imageName = $"{folder}/{GetRandomImageName("avatar")}";

        var blobClient = blobContainer.GetBlobClient(imageName);
        var uploaded = await blobClient.UploadAsync(imageStream, true);

        if (uploaded?.Value is not null)
        {
            Log.UploadSucceeded(folder, imageName, uploaded.Value.ContentHash);
        }
        else
        {
            Log.UploadFailed(folder, imageName);
        }

        return blobClient.Uri;
    }

    static string GetRandomImageName(string prefix)
    {
        return $"{prefix}-{TokenCreator.CreateRandomString(16)}.png";
    }

    BlobServiceClient GetBlobServiceClient()
    {
        if (_options.Value.AccountName is { Length: > 0 } accountName)
        {
            var endpoint = new Uri($"https://{accountName}.blob.core.windows.net");
            return new BlobServiceClient(endpoint, new DefaultAzureCredential());
        }

        if (_options.Value.ConnectionString is { Length: > 0 } connectionString)
        {
            return new BlobServiceClient(connectionString);
        }

        if (_configuration.GetConnectionString("AbbotWebStorageAccount") is { Length: > 0 } oldConnectionString)
        {
            // Support legacy configuration option.
            return new BlobServiceClient(oldConnectionString);
        }

        throw new InvalidOperationException(
            "One of UserStorage:AccountName or UserStorage:ConnectionString must be provided.");
    }
}

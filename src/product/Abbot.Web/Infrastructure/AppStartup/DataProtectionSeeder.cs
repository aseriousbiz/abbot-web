using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class DataProtectionSeeder : IDataSeeder
{
    readonly IOptions<AbbotDataProtectionKeysOptions> _options;
    readonly IConfiguration _configuration;

    public DataProtectionSeeder(IOptions<AbbotDataProtectionKeysOptions> options, IConfiguration configuration)
    {
        _options = options;
        _configuration = configuration;
    }

    public async Task SeedDataAsync()
    {
        if (!_options.Value.UseBlobStorage)
        {
            return;
        }

        // Create the container for the blob storage keys, if it doesn't already exist.
        var containerName = _options.Value.StorageContainerName
                            ?? throw new InvalidOperationException(
                                "DataProtection:StorageContainerName not set in AppSettings.");

        BlobServiceClient blobServiceClient;
        if (_options.Value.UseManagedIdentity)
        {
            var accountName = _options.Value.StorageAccountName
                              ?? throw new InvalidOperationException(
                                  "DataProtection:StorageAccountName not set in AppSettings.");

            blobServiceClient = new BlobServiceClient(new($"https://{accountName}.blob.core.windows.net"), new DefaultAzureCredential());
        }
        else
        {
            var storageConnectionString = _configuration.GetConnectionString("AbbotWebStorageAccount")
                                          ?? throw new InvalidOperationException(
                                              $"The connection string `AbbotWebStorageAccount` is not set in AppSettings.");

            blobServiceClient = new BlobServiceClient(storageConnectionString);
        }

        var container = blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();
    }

    public bool Enabled => true;
}

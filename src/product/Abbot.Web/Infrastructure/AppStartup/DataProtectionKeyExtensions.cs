using System;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serious.Abbot.Infrastructure.AppStartup;

namespace Serious.Abbot.Configuration;

public static class DataProtectionKeyExtensions
{
    // Name of the connection string setting for the Azure Storage account where the abbot-web data keys are stored.
    const string AbbotWebStorageAccount = "AbbotWebStorageAccount";

    /// <summary>
    /// Sets up the ASP.NET Core data protection keys to be stored and encrypted at rest in an Azure Storage
    /// Blob Container. This ensures that swapping stage to prod won't log out all the users.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="hostEnvironment">The host environment.</param>
    public static void AddDataProtectionKeys(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var section = configuration.GetSection(AbbotDataProtectionKeysOptions.SectionName);

        // We configure AbbotDataProtectionKeysOptions with the config section,
        // but we also need it right now and can't wait to resolve it with IOptions.
        // So we just create it manually.
        services.Configure<AbbotDataProtectionKeysOptions>(section);
        var dataKeysOptions = section.Get<AbbotDataProtectionKeysOptions>().Require();

        var dataProtectionBuilder = services.AddDataProtection();

        if (dataKeysOptions.ApplicationName is { Length: > 0 })
        {
            dataProtectionBuilder.SetApplicationName(dataKeysOptions.ApplicationName);
        }

        if (dataKeysOptions.UseBlobStorage)
        {
            // This seeder isn't in Abbot.Web.Library, so it's not registered automatically.
            // But that works out well because we only need it when we're using blob storage for data protection key storage.
            services.AddSingleton<IDataSeeder, DataProtectionSeeder>();

            var containerName = dataKeysOptions.StorageContainerName
                                ?? throw new InvalidOperationException(
                                    "DataProtection:StorageContainerName not set in AppSettings.");

            var blobName = dataKeysOptions.StorageBlobName
                           ?? throw new InvalidOperationException(
                               "DataProtection:StorageBlobName not set in AppSettings.");

            if (dataKeysOptions.UseManagedIdentity)
            {
                var accountName = dataKeysOptions.StorageAccountName
                                  ?? throw new InvalidOperationException(
                                      "DataProtection:StorageAccountName not set in AppSettings.");

                var blobUri = new Uri($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}");
                dataProtectionBuilder.PersistKeysToAzureBlobStorage(blobUri, new DefaultAzureCredential());
            }
            else
            {
                var storageConnectionString = configuration.GetConnectionString(AbbotWebStorageAccount)
                                              ?? throw new InvalidOperationException(
                                                  $"The connection string `{AbbotWebStorageAccount}` is not set in AppSettings.");

                dataProtectionBuilder.PersistKeysToAzureBlobStorage(storageConnectionString, containerName, blobName);
            }
        }
        else if (!hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Data Protection Keys must be stored in blob storage in non-development environments.");
        }

        if (dataKeysOptions.UseKeyVault && dataKeysOptions.KeyVaultKeyId is { Length: > 0 } keyId)
        {
            dataProtectionBuilder.ProtectKeysWithAzureKeyVault(new Uri(keyId), new DefaultAzureCredential());
        }
        else if (!hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Data Protection Key encryption must be enabled unless in a development environment.");
        }
    }
}

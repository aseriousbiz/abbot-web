namespace Serious.Abbot.Configuration;

/// <summary>
/// App Settings to configure how we store the ASP.NET Core Data Protection Keys in Azure Blob Storage
/// and protect those keys at rest using Azure Key Vault.
/// </summary>
public class AbbotDataProtectionKeysOptions
{
    /// <summary>
    /// The config section name.
    /// </summary>
    public static readonly string SectionName = "DataProtection";

    /// <summary>
    /// Gets or sets the name of the application to use as the base data protection purpose.
    /// Changing this value will invalidate all payloads encrypted with data protection.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// If true, then storing data keys in Azure Blob Storage is enabled. Otherwise the
    /// default behavior is used.
    /// </summary>
    public bool UseBlobStorage { get; set; }

    /// <summary>
    /// If true, then keys wll be protected by the Azure Key Vault key specified by
    /// <see cref="KeyVaultKeyId"/>
    /// </summary>
    public bool UseKeyVault { get; set; }

    /// <summary>
    /// If true, uses Azure Managed Identity to authenticate to Azure Blob Storage.
    /// </summary>
    public bool UseManagedIdentity { get; set; }

    /// <summary>
    /// The ID of the encryption key in the Azure Key Vault. This is the key used to protect the data protection
    /// keys. 
    /// </summary>
    /// <remarks>
    /// It takes the form: https://{KEY_VAULT_NAME}.vault.azure.net/keys/{KEY_NAME}/{VERSION_STRING}
    /// </remarks>
    public string? KeyVaultKeyId { get; init; }

    /// <summary>
    /// The name of the storage account to connect to, using managed identity.
    /// </summary>
    public string? StorageAccountName { get; set; }

    /// <summary>
    /// Name of the BLOB Container in the storage account where the data protection keys are stored.
    /// </summary>
    /// <remarks>
    /// In our case, "abbot-web-storage-keys"
    /// </remarks>
    public string? StorageContainerName { get; init; }

    /// <summary>
    /// Name of the BLOB (file) that contains the keys.
    /// </summary>
    public string? StorageBlobName { get; init; }
}

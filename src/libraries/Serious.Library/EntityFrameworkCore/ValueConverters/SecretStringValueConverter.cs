using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Serious.Cryptography;

namespace Serious.EntityFrameworkCore.ValueConverters;

/// <summary>
/// A value converter used to convert <see cref="SecretString"/> to and from a <see cref="string"/>. When reading
/// a value from the database, the value is not decrypted. When storing, it is encrypted if it needs it.
/// </summary>
/// <remarks>
/// A <c>null</c> value will never be passed to a value converter by EF Core. See
/// <see href="https://docs.microsoft.com/en-us/ef/core/modeling/value-conversions?tabs=data-annotations"/>
/// </remarks>
public class SecretStringValueConverter : ValueConverter<SecretString, string>
{
    /// <summary>
    /// Constructs a <see cref="SecretStringValueConverter"/> with the specified
    /// <see cref="IDataProtectionProvider"/>.
    /// </summary>
    /// <param name="dataProtectionProvider">The <see cref="IDataProtectionProvider"/> to use for encryption.</param>
    /// <param name="migrateApiKeysOnSave">
    /// If <c>true</c>, we'll check to see if the secure string needs migration (aka the ASP.NET Core Data Protection
    /// Provider keys used to encrypt it are expired or have been revoked). If it does need migration, we'll migrate it
    /// by re-encrypting the value. This can be a costly operation so only set this to <c>true</c> if you do not have
    /// a scheduled job that handles key migration.</param>
    public SecretStringValueConverter(IDataProtectionProvider dataProtectionProvider, bool migrateApiKeysOnSave = false)
        : base(
            secret => StoreSecret(secret, migrateApiKeysOnSave),
            storedValue => new SecretString(storedValue, dataProtectionProvider))
    {
    }

    // If we allow migrating keys on save, we'll check if the secret needs migration and migrate it.
    // Checking to see if it needs migration is expensive.
    static string StoreSecret(SecretString secret, bool migrateApiKeys)
    {
        return migrateApiKeys && secret.RequiresMigration
            ? secret.Migrate().ProtectedValue
            : secret.ProtectedValue;
    }
}

using System;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Serious.EntityFrameworkCore.ValueConverters;

namespace Serious.Cryptography;

/// <summary>
/// Wraps a string and provides methods to encrypt and decrypt it using the ASP.NET Core Data Protection System.
/// Can be combined with an EF Core Value Provider to ensure that the encrypted value is stored in the database.
/// </summary>
/// <remarks>
/// <para>
/// When encrypted, the format of the secret is <c>{serious-shh:{nonce}:{base64(encrypted)}</c>.
/// </para>
/// <para>
/// The nonce is a random string that is supplied as the <c>purpose</c> to the ASP.NET Core Data Protection system
/// when encrypting a value. If the SecretString requires migration, call <see cref="Migrate"/> to decrypt the value
/// with the current nonce, generate a new nonce, and then encrypt the data with the new nonce.
/// </para>
/// </remarks>
[JsonConverter(typeof(SecretStringConverter))]
public class SecretString : IEquatable<SecretString>
{
    static IDataProtectionProvider? _defaultDataProtectionProvider;

    public static void Configure(IDataProtectionProvider dataProtectionProvider)
    {
        _defaultDataProtectionProvider = dataProtectionProvider;
    }

    public static readonly SecretString EmptySecret = new(string.Empty, new DesignTimeDataProtectionProvider());

    static readonly UTF8Encoding SecureUtf8Encoding = new(false, true);

    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly EncryptedSecret _encryptedSecret;
    readonly Lazy<PlainTextSecret> _unprotectedValue; // null if not yet decrypted

    /// <summary>
    /// Constructs a <see cref="SecretString"/> with the provided value. If the value is already a protected value,
    /// this just wraps it. If it's not a protected value, this protects it.
    /// </summary>
    /// <param name="value">The value to protect.</param>
    public SecretString(string value) : this(value, _defaultDataProtectionProvider.Require())
    {
    }

    /// <summary>
    /// Constructs a <see cref="SecretString"/> with the provided value. If the value is already a protected value,
    /// this just wraps it. If it's not a protected value, this protects it.
    /// </summary>
    /// <param name="value">The value to protect.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt and decrypt the stored value.</param>
    public SecretString(string value, IDataProtectionProvider dataProtectionProvider)
        : this(EncryptedSecret.ParseValue(value), dataProtectionProvider)
    {
    }

    SecretString(EncryptedSecret encryptedSecret, IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
        if (encryptedSecret.IsPlainText)
        {
            // We'll protect it here so it does not need migration.
            _unprotectedValue = new Lazy<PlainTextSecret>(() => new PlainTextSecret(encryptedSecret.EncryptedPart, false));
            _encryptedSecret = Protect(encryptedSecret.EncryptedPart, dataProtectionProvider);
        }
        else
        {
            _unprotectedValue = new Lazy<PlainTextSecret>(() => GetRevealedSecret(encryptedSecret, dataProtectionProvider));
            _encryptedSecret = encryptedSecret;
        }
    }

    static EncryptedSecret Protect(string value, IDataProtectionProvider dataProtectionProvider)
    {
        var nonce = EncryptedSecret.CreateNonce();
        var protector = dataProtectionProvider.CreateProtector(nonce);
        return new EncryptedSecret(protector.Protect(value), nonce);
    }

    /// <summary>
    /// Unprotects the secret and returns the plain-text value. Use this when you actually need to work with the
    /// secret value.
    /// </summary>
    public string Reveal()
    {
        return _unprotectedValue.Value.Value;
    }

    static PlainTextSecret GetRevealedSecret(EncryptedSecret encryptedSecret, IDataProtectionProvider dataProtectionProvider)
    {
        if (encryptedSecret.EncryptedPart.Length is 0)
        {
            return new PlainTextSecret(string.Empty, false);
        }

        var (encryptedValue, nonce) = encryptedSecret;
        var protectedBytes = WebEncoders.Base64UrlDecode(encryptedValue);
        var protector = dataProtectionProvider.CreateProtector(nonce) as IPersistedDataProtector
            ?? throw new InvalidOperationException("The data protection provider does not support IPersistedDataProtector.");

        var decryptedBytes = protector.DangerousUnprotect(
            protectedBytes,
            ignoreRevocationErrors: true,
            out var requiresMigration,
            out var wasRevoked);

        var plainTextValue = SecureUtf8Encoding.GetString(decryptedBytes);

        return new PlainTextSecret(plainTextValue, requiresMigration || wasRevoked);
    }

    /// <summary>
    /// Returns a new <see cref="Secret"/> with the same protected value, but encrypted with a new nonce and
    /// the current encryption keys.
    /// </summary>
    public SecretString Migrate()
    {
        // Decrypt using current nonce if it's not already decrypted.
        return new SecretString(_unprotectedValue.Value.Value, _dataProtectionProvider); // Encrypt using new nonce.
    }

    /// <summary>
    /// Whether or not this <see cref="SecretString"/> has an empty value.
    /// </summary>
    public bool Empty => _encryptedSecret.EncryptedPart.Length is 0;

    /// <summary>
    /// Retrieves the protected secret in the standard format <c>{serious-shh:{nonce}:{base64(encrypted)}</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the value hasn't been protected.</exception>
    public string ProtectedValue => _encryptedSecret.ToString();

    /// <summary>
    /// Returns <c>true</c> if the protected value either is a plain-text value and needs to be protected, or if
    /// the keys have been expired or revoked and this secret needs to be migrated.
    /// </summary>
    public bool RequiresMigration =>
        // The only way we know if it needs migration is to actually call try and unprotect it
        // and see what the ASP.NET Core Data Protection Provider tells us.
        _unprotectedValue.Value.RequiresMigration;

    /// <summary>
    /// Represents an unprotected secret value. If <see cref="RequiresMigration"/> is <c>true</c>, then the secret needs
    /// to be migrated before being stored again.
    /// </summary>
    /// <param name="Value">The plain-text value.</param>
    /// <param name="RequiresMigration">Whether or not the <see cref="SecretString"/> needs to be migrated.</param>
    record PlainTextSecret(string Value, bool RequiresMigration);

    /// <summary>
    /// Represents the component parts of a value protected by a <see cref="SecretString"/>.
    /// </summary>
    /// <param name="EncryptedPart">The protected value part.</param>
    /// <param name="Nonce">The nonce used to encrypt the value.</param>
    readonly record struct EncryptedSecret(string EncryptedPart, string? Nonce)
    {
        const string EncryptedStringPrefix = "serious-shhh"; // Never change this.
        const int NonceLength = 8; // Never change this.
        static readonly int PrefixLength = EncryptedStringPrefix.Length;
        static readonly int HeaderLength = PrefixLength + NonceLength + 2; // {prefix}:{nonce}:...

        /// <summary>
        /// Parses the protected value into its constituent parts and checks to make sure it starts with the
        /// correct prefix. If the value is plain-text, then the nonce is null.
        /// </summary>
        /// <param name="value">The value that should be protected.</param>
        /// <returns>A parsed protected value.</returns>
        public static EncryptedSecret ParseValue(string value)
        {
            if (value.Length <= HeaderLength)
            {
                // This must be plain-text. We better protect it.
                return new EncryptedSecret(value, null);
            }

            // Since the header components are fixed-width, we can use range indexers.
            var firstDelimiterIndex = PrefixLength;
            var secondDelimiterIndex = HeaderLength - 1;
            var prefix = value[..firstDelimiterIndex];
            var delimiter = value[firstDelimiterIndex];
            var nonce = value[(firstDelimiterIndex + 1)..secondDelimiterIndex];
            var secondDelimiter = value[secondDelimiterIndex];
            var encrypted = value[HeaderLength..];

            if (delimiter is not ':'
                || secondDelimiter is not ':'
                || nonce.Length != NonceLength
                || !prefix.Equals(EncryptedStringPrefix, StringComparison.Ordinal))
            {
                // This must be plain-text. We better protect it.
                return new EncryptedSecret(value, null);
            }

            return new EncryptedSecret(encrypted, nonce);
        }

        public static string CreateNonce() => TokenCreator.CreateRandomString(NonceLength);

        /// <summary>
        /// Whether or not this value needs to be protected. Empty strings do not need protection.
        /// Plain text values (those without a nonce), do need protection.
        /// </summary>
        public bool IsPlainText => Nonce is null && EncryptedPart is { Length: > 0 };

        public override string ToString() => EncryptedPart.Length is 0
            ? string.Empty
            : $"{EncryptedStringPrefix}:{Nonce}:{EncryptedPart}";
    }

    public bool Equals(SecretString? other) =>
        other is not null && string.Equals(ProtectedValue, other.ProtectedValue, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SecretString other && Equals(other);

    public override int GetHashCode() => ProtectedValue.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => ProtectedValue;

    /// <summary>
    /// Implicit cast from string to <see cref="SecretString"/>.
    /// </summary>
    /// <param name="value">The value to make or keep secret.</param>
    public static implicit operator SecretString(string? value) =>
        value is { Length: > 0 }
            ? new(value)
            : EmptySecret;
}

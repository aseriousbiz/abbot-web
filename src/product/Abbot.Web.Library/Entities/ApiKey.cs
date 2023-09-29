using System;
using Serious.Cryptography;

namespace Serious.Abbot.Entities;

/// <summary>
/// An ApiKey used to access the Abbot API via the Abbot Command-Line tool
/// </summary>
public class ApiKey : EntityBase<ApiKey>
{
    /// <summary>
    /// User friendly name to remember the key by.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The Id of the owner of the API Key
    /// </summary>
    public int OwnerId { get; set; }

    /// <summary>
    /// The owner of the API Key
    /// </summary>
    public Member Owner { get; set; } = null!;

    /// <summary>
    /// The API key token
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// The number of days since the token was last created that it expires in.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Whether or not this API key is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpirationDate;

    /// <summary>
    /// The date (UTC) when this API key expires.
    /// </summary>
    public DateTime ExpirationDate => Created.AddDays(ExpiresIn);
}

public static class ApiKeyExtensions
{
    /// <summary>
    /// Gets a masked version of the key suitable for display.
    /// </summary>
    /// <param name="key">The <see cref="ApiKey"/> to get a masked string for.</param>
    /// <returns>A masked version suitable for display.</returns>
    public static string GetMaskedToken(this ApiKey key)
    {
        var prefix = key.Token[..8];
        var padding = new string('*', (TokenCreator.DefaultApiTokenLength + TokenCreator.ChecksumLength) - prefix.Length);
        return $"{prefix}{padding}";
    }
}

using System;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Serious.Cryptography;

public static class TokenCreator
{
    public const int DefaultApiTokenLength = 24;
    public const int ChecksumLength = 6;
    internal const string TokenAlphabet = "-_~bcdfghjlmnpqrstvwxyzBCDFGHJLMNPQRSTVWXYZ123456789";

    // Omit vowels to avoid shitty words being generated
    const string SanitizedAlphabet = "bcdfghjlmnpqrstvwxyzBCDFGHJLMNPQRSTVWXYZ";

    public static string CreateRandomString(int length)
    {
        return SanitizedAlphabet.CreateRandomString(length);
    }

    static string CreateRandomString(this string source, int length)
    {
        return new(Enumerable.Repeat(0, length)
            .Select(_ => source[RandomNumberGenerator.GetInt32(source.Length)])
            .ToArray());
    }

    public static string CreateStrongToken()
    {
        return TokenAlphabet.CreateRandomString(DefaultApiTokenLength);
    }

    public static string CreateStrongAuthenticationToken(string prefix)
    {
        return CreateStrongAuthenticationToken(prefix, DefaultApiTokenLength);
    }

    /// <summary>
    /// Creates a strong token with a prefix and a checksum in the same manner that GitHub
    /// does with its authentication tokens.
    /// </summary>
    /// <param name="prefix">The prefix to prepend to the token.</param>
    /// <param name="tokenLength">The length of the random part of the token. The resulting token length will be this + the length of the prefix + 7.</param>
    public static string CreateStrongAuthenticationToken(string prefix, int tokenLength)
    {
        if (prefix is { Length: 0 })
        {
            throw new ArgumentException("Prefix must be at least one character", nameof(prefix));
        }

        if (tokenLength < DefaultApiTokenLength)
        {
            throw new ArgumentException($"Strong tokens should be {DefaultApiTokenLength} or longer.", nameof(tokenLength));
        }

        if (prefix[^1] == '_')
        {
            prefix = prefix[..^1];
        }
        var randomPart = TokenAlphabet.CreateRandomString(DefaultApiTokenLength);
        var checksum = ComputeChecksum(randomPart);
        return $"{prefix}_{randomPart}{checksum}";
    }

    /// <summary>
    /// Returns true if the token is formatted in a valid manner. This means it has the correct prefix and
    /// the last six characters are a checksum of the random part of the token.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="randomPartLength">The expected length of the random part of the token.</param>
    /// <param name="prefix">The expected token prefix.</param>
    /// <returns></returns>
    public static bool IsValidTokenFormat(string token, int randomPartLength, string prefix)
    {
        if (prefix is { Length: 0 })
        {
            throw new ArgumentException("Prefix must be at least one character", nameof(prefix));
        }
        if (prefix[^1] == '_')
        {
            prefix = prefix[..^1];
        }
        // Tokens have a prefix, underscore character, random part, and 6 character checksum.
        if (token.Length != prefix.Length + 1 + randomPartLength + 6)
        {
            return false;
        }

        if (!token.StartsWith(prefix + "_", StringComparison.Ordinal))
        {
            return false;
        }

        var randomPart = token[(prefix.Length + 1)..^6];
        var checksum = token[^6..];
        var expectedChecksum = ComputeChecksum(randomPart);

        return checksum.Equals(expectedChecksum, StringComparison.Ordinal);
    }

    static string ComputeChecksum(string randomPart)
    {
        return Checksum.ComputeCrc32Base62Checksum(randomPart, ChecksumLength, '0');
    }

    /// <summary>
    /// Creates a random token that will only be shared between machines (i.e. doesn't need to be copy-pasted or transcribed).
    /// Unlike the other tokens, this token is not limited to a specific alphabet designed for user-visible tokens.
    /// This is just 256 bits (by default) of sweet sweet randomness, Base64Url encoded.
    /// </summary>
    /// <param name="byteLength">The number of random bytes to generate for this token.</param>
    public static string CreateMachineToken(int byteLength = 256 / 8)
    {
        // Just a nice simple 256-bit token
        var buf = new byte[byteLength];
        RandomNumberGenerator.Fill(buf);

        // Use base64url encoding to make the token URL-safe.
        return $"abbot.v1.{WebEncoders.Base64UrlEncode(buf)}";
    }
}

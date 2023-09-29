#pragma warning disable CA5350
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Serious.Cryptography;

public static class HashExtensions
{
    // ReSharper disable once InconsistentNaming
    public static string ComputeSHA1Hash(this string value)
    {
        // Using this for checksums
        var encoded = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(encoded);
    }

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once IdentifierTypo
    public static string ComputeHMACSHA256Hash(this string value, string secret)
    {
        var encoded = value.ComputeHMACSHA256HashBytes(secret);
        return Convert.ToBase64String(encoded);
    }

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once IdentifierTypo
    public static byte[] ComputeHMACSHA256HashBytes(this string value, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(key);
        var bytes = Encoding.UTF8.GetBytes(value);
        return hmac.ComputeHash(bytes);
    }

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once IdentifierTypo
    public static string ComputeHMACSHA256FileName(this string value, string secret)
    {
        var encoded = value.ComputeHMACSHA256HashBytes(secret);
        return string.Concat(encoded.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    const int FnvPrime = 16777619;

    /// <summary>
    /// Generates a Fowler–Noll–Vo hash function. This is not meant for cryptographic situations. It's useful
    /// for checksums and hash tables.
    /// </summary>
    /// <param name="value">The value to compute the hash of.</param>
    /// <returns>The FNV hash</returns>
    public static int ComputeFowlerNollVoHash(int value)
    {
        const int offsetBasis = unchecked((int)2166136261);

        int hash = offsetBasis;
        Combine(ref hash, unchecked((byte)value));
        Combine(ref hash, unchecked((byte)(value >> 8)));
        Combine(ref hash, unchecked((byte)(value >> 16)));
        Combine(ref hash, unchecked((byte)(value >> 24)));
        return hash;
    }

    static void Combine(ref int hash, byte data)
    {
        unchecked
        {
            hash ^= data;
            hash *= FnvPrime;
        }
    }
}

using System.Text;
using Serious.Text;

namespace Serious.Cryptography;

public static class Checksum
{
    /// <summary>
    /// Computes a 32-bit checksum on the value and Base32 encodes the result as a padded string.
    /// </summary>
    /// <param name="value">The value to calculate the checksum for.</param>
    /// <param name="minLength">The minimum length for the checksum.</param>
    /// <param name="padCharacter">The left-padded character if the checksum is smaller than minLength.</param>
    /// <returns></returns>
    public static string ComputeCrc32Base62Checksum(string value, int minLength, char padCharacter)
    {
        return BaseEncoder.ToBase62(Crc32.Compute(value, Encoding.UTF8))
            .PadLeft(minLength, padCharacter);
    }
}

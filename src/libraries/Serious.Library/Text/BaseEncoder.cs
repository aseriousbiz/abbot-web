namespace Serious.Text;

public static class BaseEncoder
{
    /// <summary>
    /// Converts a number to base 62 number. Note that this is not the same as encoding a string.
    /// </summary>
    /// <param name="number">The number to convert.</param>
    /// <returns>A base62 version of the number.</returns>
    // Credit: https://stackoverflow.com/a/33320388
    public static string ToBase62(ulong number)
    {
        const string? alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var n = number;
        const ulong basis = 62;
        var ret = "";
        while (n > 0)
        {
            ulong temp = n % basis;
            ret = alphabet[(int)temp] + ret;
            n /= basis;

        }
        return ret;
    }
}

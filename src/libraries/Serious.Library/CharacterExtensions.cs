namespace Serious;

public static class CharacterExtensions
{
    public static bool IsAsciiLetter(this char c)
    {
        return c switch
        {
            _ when c >= 'a' && c <= 'z' => true,
            _ when c >= 'A' && c <= 'Z' => true,
            _ => false
        };
    }

    public static bool IsAsciiAlphaNumeric(this char c)
    {
        return char.IsDigit(c) || c.IsAsciiLetter();
    }
}

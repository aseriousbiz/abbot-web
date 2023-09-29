namespace Serious.TestHelpers
{
    public static class StringExtensions
    {
        public static string NormalizeLineEndings(this string original, bool trim = false) =>
            trim ? original.Replace("\r\n", "\n").Trim() : original.Replace("\r\n", "\n");
    }
}

using Serious.Logging;

namespace Serious.TestHelpers;

public class FakeSensitiveLogDataProtector : ISensitiveLogDataProtector
{
    const string Prefix = "PROTECTED:";

    public string? Protect(string? content) => Prefix + content;

    public string Unprotect(string cipherText) =>
        cipherText.StartsWith(Prefix, StringComparison.Ordinal)
            ? cipherText[Prefix.Length..]
            : throw new ArgumentException($"Expected {Prefix} prefix");
}

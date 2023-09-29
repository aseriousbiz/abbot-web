using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.DataProtection;

namespace Serious.Logging;

public interface ISensitiveLogDataProtector
{
    /// <inheritdoc cref="IDataProtector.Protect(byte[])" />
    /// <param name="content">The plain text to protect.</param>
    [return: NotNullIfNotNull(nameof(content))]
    string? Protect(string? content);

    /// <inheritdoc cref="IDataProtector.Unprotect(byte[])" />
    /// <param name="cipherText">The protected data.</param>
    string Unprotect(string cipherText);

    /// <summary>
    /// An <see cref="ISensitiveLogDataProtector"/> that does not protect data.
    /// </summary>
    public static ISensitiveLogDataProtector Null { get; } = new NullSensitiveLogDataProtector();

    class NullSensitiveLogDataProtector : ISensitiveLogDataProtector
    {
        [return: NotNullIfNotNull("content")]
        public string? Protect(string? content) => content;

        public string Unprotect(string cipherText) => cipherText;
    }
}

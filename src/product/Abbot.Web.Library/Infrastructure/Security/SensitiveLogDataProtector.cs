using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.Security;

public class SensitiveLogDataProtector : ISensitiveLogDataProtector
{
    private IHostEnvironment _hostEnvironment;
    private IDataProtector _dataProtector;

    public SensitiveLogDataProtector(
        IHostEnvironment hostEnvironment,
        IDataProtectionProvider dataProtectionProvider)
    {
        _hostEnvironment = hostEnvironment;
        _dataProtector = dataProtectionProvider.CreateProtector("Sensitive-Log-Data");
    }

    [return: NotNullIfNotNull(nameof(content))]
    public string? Protect(string? content)
    {
        if (content is null || _hostEnvironment.IsDevelopment())
        {
            return content;
        }

        return _dataProtector.Protect(content);
    }

    public string Unprotect(string cipherText)
    {
        return _dataProtector.Unprotect(cipherText);
    }
}

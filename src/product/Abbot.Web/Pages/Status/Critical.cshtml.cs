using Microsoft.Extensions.Logging;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Pages.Status;

public class CriticalPageModel : UserPage
{
    static readonly ILogger<CriticalPageModel> Log = ApplicationLoggerFactory.CreateLogger<CriticalPageModel>();

    public void OnGet()
    {
        if (User.IsInRole(Roles.Staff))
        {
            Log.TestCriticalEvent(Viewer.DisplayName);
        }
    }
}

public static partial class CriticalPageModelLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Critical,
        Message = "Testing critical log event: {DisplayName}.")]
    public static partial void TestCriticalEvent(this ILogger<CriticalPageModel> logger, string displayName);
}

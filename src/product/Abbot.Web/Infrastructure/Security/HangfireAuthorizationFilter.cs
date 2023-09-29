using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Serious.Abbot.Infrastructure.Security;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return context.GetHttpContext().IsStaffMode();
    }
}

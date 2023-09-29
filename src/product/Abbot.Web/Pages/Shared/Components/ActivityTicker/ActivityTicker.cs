using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Extensions;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Components;

[ViewComponent]
public class ActivityTicker : ViewComponent
{
    readonly IAuditLogReader _auditLog;

    public ActivityTicker(IAuditLogReader auditLog)
    {
        _auditLog = auditLog;
    }

    public async Task<IViewComponentResult> InvokeAsync(int count)
    {
        var organization = HttpContext.RequireCurrentOrganization();

        {
            var auditEvents = await _auditLog.GetRecentActivityAsync(
                organization,
                count);
            return View(auditEvents);
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Activity;

public class IndexModel : ActivityPageBase
{
    public IndexModel(IAuditLogReader auditLog, IUserRepository userRepository) : base(auditLog, userRepository)
    {
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Activity/Index", new { Id = Organization.PlatformId });

    public Task OnGetAsync(
        int? p = null,
        StatusFilter? filter = null,
        ActivityTypeFilter? type = null,
        string? range = null)
    {
        return GetAsync(
            p ?? 1,
            filter ?? StatusFilter.All,
            type ?? ActivityTypeFilter.User,
            range ?? string.Empty);
    }
}

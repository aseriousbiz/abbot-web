using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Activity;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations.Activity;

public class DetailsPage : DetailsModel
{
    public DetailsPage(IAuditLogReader auditLogReader, IAuditLog auditLog, IOrganizationRepository organizationRepository, AbbotContext db)
        : base(auditLogReader, auditLog, organizationRepository, db)
    {
    }

    public new Organization Organization { get; private set; } = null!;

    protected override async Task<(AuditEventBase? AuditEvent, User? User)> InitializePageAsync(Guid id)
    {
        var (auditEvent, user) = await base.InitializePageAsync(id);
        if (auditEvent is { Organization: { } organization })
        {
            if (organization.PlatformId != RouteData.Values["orgId"] as string)
            {
                return (null, null);
            }

            Organization = organization;
            ViewData.SetOrganization(organization);
        }
        return (auditEvent, user);
    }
}

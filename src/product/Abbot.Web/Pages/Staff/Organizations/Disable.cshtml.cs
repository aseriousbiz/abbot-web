using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class DisablePage : OrganizationDetailPage
{
    static readonly ILogger<DisablePage> Log = ApplicationLoggerFactory.CreateLogger<DisablePage>();

    public DisablePage(AbbotContext db, IAuditLog auditLog)
        : base(db, auditLog)
    {
    }

    public async Task OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var organization = await InitializeDataAsync(id);
        Expect.True(!organization.IsSerious());
        organization.Enabled = false;
        await Db.SaveChangesAsync();

        await AuditLog.LogAdminActivityAsync("Disabled organization.", Viewer.User, organization);
        Log.OrganizationDisabled(organization.Id, organization.Name, Viewer.UserId, User.GetPlatformUserId());
        StatusMessage = $"Organization {organization.Name} disabled";

        return RedirectToPage("../Index");
    }

    protected override Task InitializeDataAsync(Entities.Organization organization)
    {
        return Task.CompletedTask;
    }
}

static partial class DisabledPageLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message =
            "Organization disabled (Id: {OrganizationId}, Name: {OrganizationName}, Disabled By: {UserId} ({PlatformUserId}))")]
    public static partial void OrganizationDisabled(
        this ILogger<DisablePage> logger,
        int organizationId,
        string? organizationName,
        int userId,
        string? platformUserId);
}

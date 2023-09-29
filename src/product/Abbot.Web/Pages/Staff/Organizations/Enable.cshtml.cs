using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class EnablePage : OrganizationDetailPage
{
    static readonly ILogger<EnablePage> Log = ApplicationLoggerFactory.CreateLogger<EnablePage>();

    public EnablePage(AbbotContext db, IAuditLog auditLog)
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
        organization.Enabled = true;
        await Db.SaveChangesAsync();

        await AuditLog.LogAdminActivityAsync("Enabled organization.", Viewer.User, organization);
        Log.OrganizationEnabled(organization.Id, organization.Name, Viewer.UserId, User.GetPlatformUserId());
        StatusMessage = $"Organization {organization.Name} enabled";

        return RedirectToPage("../Index");
    }

    protected override Task InitializeDataAsync(Entities.Organization organization)
    {
        return Task.CompletedTask;
    }
}

public static partial class EnablePageLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message =
            "Organization enabled (Id: {OrganizationId}, Name: {OrganizationName}, Enabled By: {UserId} ({PlatformUserId}))")]
    public static partial void OrganizationEnabled(
        this ILogger logger,
        int organizationId,
        string? organizationName,
        int userId,
        string? platformUserId);
}

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class DeletePage : OrganizationDetailPage
{
    static readonly ILogger<DeletePage> Log = ApplicationLoggerFactory.CreateLogger<DeletePage>();
    readonly IOrganizationRepository _organizationRepository;
    readonly IHostEnvironment _hostEnvironment;

    [BindProperty]
    public string Reason { get; set; } = "Customer asked us to delete their organization";

    public long AuditEventsCount { get; private set; }

    public DeletePage(
        IOrganizationRepository organizationRepository,
        AbbotContext db,
        IAuditLog auditLog,
        IHostEnvironment hostEnvironment)
        : base(db, auditLog)
    {
        _organizationRepository = organizationRepository;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var organization = await InitializeDataAsync(id);

        if (_hostEnvironment.IsProduction() && organization.Enabled)
        {
            return NotFound();
        }


        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var organization = await InitializeDataAsync(id);

        if (_hostEnvironment.IsProduction() && organization.Enabled)
        {
            return NotFound();
        }

        try
        {
            await _organizationRepository.DeleteOrganizationAsync(id, Reason, Viewer);

            Log.OrganizationDeleted(organization.Id, organization.Name, Viewer.DisplayName, User.GetPlatformUserId());
            StatusMessage = $"Organization {organization.Name} deleted";
            return RedirectToPage("../Index");
        }
        catch (Exception ex)
        {
            Log.ErrorDeletingOrganization(ex, organization.Id, organization.Name, Viewer.DisplayName, User.GetPlatformUserId());
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Error deleting organization {organization.Name}\n{ex.Message}\n{ex.InnerException?.Message}";
        }

        return RedirectToPage();
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        AuditEventsCount = await Db.AuditEvents
            .Where(e => e.OrganizationId == organization.Id || (e.ActorMember != null && e.ActorMember.OrganizationId == organization.Id))
            .CountAsync();
    }
}

static partial class DeletePageLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message =
            "Organization deleted (Id: {OrganizationId}, Name: {OrganizationName}, Deleted By: {ActorDisplayName} ({PlatformUserId}))")]
    public static partial void OrganizationDeleted(
        this ILogger<DeletePage> logger,
        int organizationId,
        string? organizationName,
        string actorDisplayName,
        string? platformUserId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Error deleting Organization (Id: {OrganizationId}, Name: {OrganizationName}, Deleted By: {ActorDisplayName} ({PlatformUserId}))")]
    public static partial void ErrorDeletingOrganization(
        this ILogger<DeletePage> logger,
        Exception exception,
        int organizationId,
        string? organizationName,
        string actorDisplayName,
        string? platformUserId);
}

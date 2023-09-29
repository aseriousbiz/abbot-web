using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class EditPage : OrganizationDetailPage
{
    [BindProperty]
    [RegularExpression(@"^[\w]{1,15}$")]
    public string? Slug { get; set; }

    [BindProperty]
    public PlanType? PlanType { get; set; }

    [BindProperty]
    public bool SlugIsDefault { get; set; }

    [BindProperty]
    public string? Reason { get; set; }

    public IReadOnlyList<SelectListItem> AvailablePlans { get; set; } = null!;

    public EditPage(AbbotContext db, IAuditLog auditLog)
        : base(db, auditLog)
    {
    }

    public async Task OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var newSlug = Slug ?? string.Empty;
        var slugIsDefault = SlugIsDefault;
        var newPlanType = PlanType;
        var reason = Reason ?? string.Empty;

        var organization = await InitializeDataAsync(id);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var status = new List<string>();

        if (organization.PlatformType is not PlatformType.Slack)
        {
            if (slugIsDefault)
            {
                organization.Slug = organization.PlatformId;
                var slugMessage = $"Organization Slug reset to default ({organization.PlatformId}).";
                await AuditLog.LogAuditEventAsync(new()
                {
                    Type = new("Organization.Slug", "Reset"),
                    Actor = Viewer,
                    Organization = organization,
                    Description = slugMessage,
                    StaffPerformed = true,
                    StaffOnly = true,
                });

                status.Add(slugMessage);
            }
            else
            {
                organization.Slug = newSlug;
                var slugMessage = $"Organization Slug set to `{newSlug}`.";
                await AuditLog.LogAuditEventAsync(new()
                {
                    Type = new("Organization.Slug", "Changed"),
                    Actor = Viewer,
                    Organization = organization,
                    Description = slugMessage,
                    StaffPerformed = true,
                    StaffOnly = true,
                });

                status.Add(slugMessage);
            }
        }

        if (status.Count > 0)
        {
            StatusMessage = string.Join(" ", status);
        }

        await Db.SaveChangesAsync();

        return RedirectToPage();
    }

    protected override Task InitializeDataAsync(Entities.Organization organization)
    {
        AvailablePlans = Plan.AllTypes.Select(t => new SelectListItem(t.ToString(), t.ToString())).ToList();
        PlanType = organization.PlanType;
        Slug = organization.Slug;
        SlugIsDefault = organization.Slug == organization.PlatformId;
        return Task.CompletedTask;
    }
}

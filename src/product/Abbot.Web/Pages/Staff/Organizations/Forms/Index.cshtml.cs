using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations.Forms;

public class IndexModel : OrganizationDetailPage
{
    public IList<SelectListItem> AvailableForms { get; set; } = new List<SelectListItem>();

    public IndexModel(AbbotContext db, IAuditLog auditLog) : base(db,
        auditLog)
    {
    }

    public async Task OnGet(string id)
    {
        await InitializeDataAsync(id);
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        AvailableForms = SystemForms.Definitions.Keys
            .OrderBy(k => k)
            .Select(k => new SelectListItem(k,
                Url.Page("Edit",
                    new {
                        id = organization.PlatformId,
                        form = k
                    }))).ToList();
        AvailableForms.Insert(0, new SelectListItem("---", string.Empty, false, true));
        AvailableForms.Insert(0, new SelectListItem("<No Form Selected>", string.Empty));
    }
}

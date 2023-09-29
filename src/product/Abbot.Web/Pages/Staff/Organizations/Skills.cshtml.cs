using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class SkillsPage : OrganizationDetailPage
{
    public IReadOnlyList<Skill> Skills { get; private set; } = null!;

    public SkillsPage(AbbotContext db, IAuditLog auditLog)
        : base(db, auditLog)
    {
    }

    public async Task OnGetAsync(string id)
    {
        await base.InitializeDataAsync(id);
    }

    protected override Task InitializeDataAsync(Entities.Organization organization)
    {
        Skills = organization.Skills;
        return Task.CompletedTask;
    }
}

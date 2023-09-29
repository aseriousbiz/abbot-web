using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations
{
    public abstract class OrganizationDetailPage : StaffToolsPage
    {
        protected IAuditLog AuditLog { get; }

        protected AbbotContext Db { get; }

        public Organization Organization { get; private set; } = null!;

        protected OrganizationDetailPage(AbbotContext db, IAuditLog auditLog)
        {
            AuditLog = auditLog;
            Db = db;
        }

        protected abstract Task InitializeDataAsync(Organization organization);

        protected async Task<Organization> InitializeDataAsync(string id)
        {
            await InitializeOrganizationAsync(id);
            await InitializeDataAsync(Organization);

            return Organization;
        }

        protected async Task InitializeOrganizationAsync(string id)
        {
            if (!HttpContext.IsStaffMode())
            {
                throw new InvalidOperationException("User must be a staff user");
            }

            Organization = await Db.Organizations
#pragma warning disable CA1304
                .Include(o => o.Members)
                .ThenInclude(m => m.User)
                .Include(o => o.Members)
                .ThenInclude(u => u.MemberRoles)
                .ThenInclude(ur => ur.Role)
                .Include(o => o.Packages)
                .ThenInclude(p => p.Versions)
                .ThenInclude(pv => pv.InstalledSkills)
                .Include(o => o.Skills)
                .AsSplitQuery()
                // ReSharper disable once SpecifyStringComparison
                .SingleAsync(o => o.PlatformId.ToLower() == id.ToLower());
#pragma warning restore CA1304

            ViewData.SetOrganization(Organization);
        }
    }
}

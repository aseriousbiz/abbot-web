using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

public class AliasRepository : OrganizationScopedRepository<Alias>, IAliasRepository
{
    public AliasRepository(AbbotContext db, IAuditLog auditLog) : base(db, auditLog)
    {
        Entities = db.Aliases;
    }

    protected override DbSet<Alias> Entities { get; }
    public async Task<Alias?> GetAsync(string name, Organization organization)
    {
        return await GetQueryable(organization)
            .SingleOrDefaultAsync(alias => alias.OrganizationId == organization.Id
                                           && alias.Name == name);
    }
}

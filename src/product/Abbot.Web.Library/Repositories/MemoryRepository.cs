using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository for memories stored via the `rem` command.
/// </summary>
public class MemoryRepository : OrganizationScopedRepository<Memory>, IMemoryRepository
{
    public MemoryRepository(AbbotContext db, IAuditLog auditLog) : base(db, auditLog)
    {
        Entities = db.Memories;
    }

    protected override DbSet<Memory> Entities { get; }

    /// <summary>
    /// Retrieves a memory by name.
    /// </summary>
    /// <param name="name">The name of the memory.</param>
    /// <param name="organization">The organization the memory belongs to.</param>
    public async Task<Memory?> GetAsync(string name, Organization organization)
    {
        return await GetQueryable(organization)
            .SingleOrDefaultAsync(memory =>
                memory.OrganizationId == organization.Id
                && memory.Name == name);
    }

    public virtual async Task<IReadOnlyList<Memory>> SearchAsync(IReadOnlyList<string> terms, Organization organization)
    {
        var termPatterns = terms.Select(t => $"%{t}%").ToList();
        return await GetQueryable(organization)
            .Where(m => termPatterns.Any(t => EF.Functions.ILike(m.Name, t)))
            .ToListAsync();
    }
}

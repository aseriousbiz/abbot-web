using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeMemoryRepository : MemoryRepository
{
    public FakeMemoryRepository(AbbotContext db, IAuditLog auditLog) : base(db, auditLog)
    {
    }

    public override async Task<IReadOnlyList<Memory>> SearchAsync(IReadOnlyList<string> terms, Organization organization)
    {
        // In-memory EF provider doesn't support ILIKE, but we can emulate it.
        var allMemories = await Entities.Where(m => m.OrganizationId == organization.Id).ToListAsync();
        return allMemories.Where(m => terms.Any(t => m.Name.Contains(t))).ToList();
    }
}

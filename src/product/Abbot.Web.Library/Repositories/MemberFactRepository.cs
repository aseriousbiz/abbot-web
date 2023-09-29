using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

/// <summary>
/// A repository for member facts. Member facts are little bits of info attached to a member of an organization
/// via the who skill.
/// </summary>
public class MemberFactRepository : Repository<MemberFact>, IMemberFactRepository
{
    readonly IAuditLog _auditLog;

    public MemberFactRepository(AbbotContext db, IAuditLog auditLog) : base(db)
    {
        _auditLog = auditLog;
    }

    /// <summary>
    /// Retrieves facts about a member of an organization.
    /// </summary>
    /// <param name="member">The member these facts are about.</param>
    /// <returns>A list of facts about the member.</returns>
    public async Task<IReadOnlyList<MemberFact>> GetFactsAsync(Member member)
    {
        return await Entities
            .Include(f => f.Subject)
            .ThenInclude(s => s.Organization)
            .Where(f => f.SubjectId == member.Id)
            .OrderByDescending(f => f.Created)
            .ThenByDescending(f => f.Id)
            .ToListAsync();
    }

    protected override DbSet<MemberFact> Entities => Db.MemberFacts;

    protected override Task LogEntityCreatedAsync(MemberFact entity, User creator)
    {
        return _auditLog.LogEntityCreatedAsync(entity, creator, entity.Subject.Organization);
    }

    protected override Task LogEntityDeletedAsync(MemberFact entity, User actor)
    {
        return _auditLog.LogEntityDeletedAsync(entity, actor, entity.Subject.Organization);
    }

    protected override Task LogEntityChangedAsync(MemberFact entity, User actor)
    {
        return Task.CompletedTask;
    }
}

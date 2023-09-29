using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

public class ListRepository : OrganizationScopedRepository<UserList>, IListRepository
{
    readonly IAuditLog _auditLog;

    public ListRepository(AbbotContext db, IAuditLog auditLog) : base(db, auditLog)
    {
        _auditLog = auditLog;
        Entities = db.UserLists;
    }

    public async Task<UserList?> GetAsync(string name, Organization organization)
    {
        return await GetQueryable(organization)
            .SingleOrDefaultAsync(list => list.OrganizationId == organization.Id
                                          && list.Name == name);
    }

    public async Task<UserListEntry> AddEntryToList(UserList list, string content, User user)
    {
        var entry = new UserListEntry
        {
            Content = content,
            Creator = user,
            ModifiedBy = user,
            List = list
        };
        list.Entries.Add(entry);
        await UpdateAsync(list, user);
        await _auditLog.LogEntityCreatedAsync(entry, user, list.Organization);
        return entry;
    }

    public async Task<bool> RemovesEntryFromList(UserList list, string content, User user)
    {
        var found = list
            .Entries
            .SingleOrDefault(item => item.Content.Equals(content, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            return false;
        }
        await _auditLog.LogEntityDeletedAsync(found, user, list.Organization);
        list.Entries.Remove(found);
        await Db.SaveChangesAsync();
        return true;
    }

    public override IQueryable<UserList> GetQueryable(Organization organization)
    {
        return base.GetQueryable(organization)
            .Include(e => e.Entries)
            .ThenInclude(e => e.Creator);
    }

    protected override DbSet<UserList> Entities { get; }
}

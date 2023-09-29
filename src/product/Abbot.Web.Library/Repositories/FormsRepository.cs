using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

public class FormsRepository : IFormsRepository
{
    readonly AbbotContext _db;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;

    public FormsRepository(AbbotContext db, IAuditLog auditLog, IClock clock)
    {
        _db = db;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<Form?> GetFormAsync(Organization organization, string key)
    {
        return await _db.Forms
            .Include(f => f.Organization)
            .SingleOrDefaultAsync(f => f.OrganizationId == organization.Id && f.Key == key);
    }

    public async Task<Form> CreateFormAsync(Organization organization, string key, string definition, bool enabled, Member actor)
    {
        var form = new Form()
        {
            Organization = organization,
            Key = key,
            Enabled = enabled,
            Definition = definition,
            Creator = actor.User,
            Created = _clock.UtcNow,
            ModifiedBy = actor.User,
            Modified = _clock.UtcNow,
        };

        await _db.Forms.AddAsync(form);
        await _db.SaveChangesAsync();

        await _auditLog.LogFormEventAsync(actor, form, "Form created", $"Created the `{key}` form");
        return form;
    }

    public async Task SaveFormAsync(Form form, Member actor)
    {
        form.Modified = _clock.UtcNow;
        form.ModifiedBy = actor.User;

        await _db.SaveChangesAsync();
        await _auditLog.LogFormEventAsync(actor, form, "Form updated", $"Updated the `{form.Key}` form");
    }

    public async Task DeleteFormAsync(Form form, Member actor)
    {
        _db.Forms.Remove(form);

        await _db.SaveChangesAsync();
        await _auditLog.LogFormEventAsync(actor, form, "Form deleted", $"Deleted the `{form.Key}` form");
    }
}

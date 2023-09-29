using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Customers;

public class DeleteAllCustomers : UserPage
{
    readonly AbbotContext _db;

    public DeleteAllCustomers(AbbotContext db)
    {
        _db = db;
    }

    public void OnGet()
    {

    }

    public async Task<IActionResult> OnPostAsync()
    {
        var relatedRuns = await _db.PlaybookRuns.Where(r => r.Related!.Customer != null && r.Playbook.OrganizationId == Organization.Id).ToListAsync();
        _db.PlaybookRuns.RemoveRange(relatedRuns);
        await _db.SaveChangesAsync();

        var roomsWithCustomers = await _db.Rooms.Where(r => r.Customer != null && r.OrganizationId == Organization.Id).ToListAsync();
        foreach (var room in roomsWithCustomers)
        {
            room.Customer = null;
        }

        await _db.SaveChangesAsync();

        var customers = await _db.Customers.Where(c => c.OrganizationId == Organization.Id).ToListAsync();
        _db.Customers.RemoveRange(customers);
        await _db.SaveChangesAsync();

        return TurboFlash("Customers deleted.");
    }
}

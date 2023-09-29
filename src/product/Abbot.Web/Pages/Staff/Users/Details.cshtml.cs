using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Users;

public class DetailsPage : StaffToolsPage
{
    readonly AbbotContext _db;

    public DetailsPage(AbbotContext db)
    {
        _db = db;
    }

    public IPaginatedList<Member> Memberships { get; set; } = null!;

    public User SubjectUser { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _db.Users
            .Include(u => u.Members)
            .ThenInclude(m => m.Organization)
            .Include(u => u.Members)
            .ThenInclude(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role)
            .SingleOrDefaultAsync(u => u.PlatformUserId == id.ToUpperInvariant());

        if (user is null)
        {
            return NotFound();
        }

        var membershipQueryable = _db.Members
            .Include(m => m.Organization)
            .Include(m => m.User)
            .Include(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role)
            .Where(m => m.UserId == user.Id);
        Memberships = await PaginatedList.CreateAsync(membershipQueryable, 1, 20);

        SubjectUser = user;
        return Page();
    }
}

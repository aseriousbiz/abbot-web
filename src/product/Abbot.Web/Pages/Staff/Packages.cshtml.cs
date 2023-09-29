using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Staff;

public class PackagesPageModel : StaffToolsPage
{
    readonly AbbotContext _db;

    public PackagesPageModel(AbbotContext db)
    {
        _db = db;
    }

    public Package Package { get; private set; } = null!;

    public ICollection<Entities.Organization> Organizations { get; private set; } = new Collection<Entities.Organization>();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!id.HasValue)
        {
            return RedirectToPage("/Packages/Index");
        }

        var package = await _db.Packages.Include(p => p.Skill).SingleOrDefaultAsync(p => p.Id == id);
        if (package is null)
        {
            return NotFound();
        }

        Package = package;

        Organizations = await _db.Skills
            .Include(s => s.Organization)
            .Include(s => s.SourcePackageVersion)
            .ThenInclude(pv => pv!.Package)
            .ThenInclude(p => p.Skill)
            .Where(s => s.SourcePackageVersion!.PackageId == id)
            .Select(s => s.Organization)
            .ToListAsync();

        return Page();
    }
}

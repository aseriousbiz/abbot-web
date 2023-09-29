using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Skills;

public class SearchPage : StaffToolsPage
{
    readonly AbbotContext _db;

    public SearchPage(AbbotContext db)
    {
        _db = db;
    }

    public string? SearchTerm { get; set; }
    public IPaginatedList<Skill>? Matches { get; set; }

    public async Task<IActionResult> OnGet(string? q, int? p = null)
    {
        if (q is null)
        {
            return Page();
        }

        var allSkills = _db.Skills
            .Include(s => s.Organization)
            .Where(Id<Skill>.TryParse(q, out var skillId)
                ? s => s.Id == skillId
                : s => s.Code.Contains(q));

        var paginated = await PaginatedList.CreateAsync(allSkills, p ?? 1, WebConstants.LongPageSize);
        Matches = paginated;
        SearchTerm = q;
        return Page();
    }
}

using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Security;

namespace Serious.Abbot.Pages.Account;

[AllowAnonymous]
public class DebugModel : PageModel
{
    readonly AbbotContext _db;

    public DebugModel(AbbotContext abbotContext)
    {
        _db = abbotContext;
    }
    public User? CurrentUser { get; private set; }
    public Member? CurrentMember { get; private set; }
    public Organization? Organization { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public string? ErrorMessage { get; private set; }


#if !DEBUG
        public IActionResult OnGet()
        {  
           return NotFound();
        }
#else
    public async Task<IActionResult> OnGet()
    {
        IsAuthenticated = User.IsAuthenticated();

        if (IsAuthenticated)
        {
            CurrentUser = await GetCurrentUserAsync(User);
        }

        var organization = HttpContext.GetCurrentOrganization();
        if (organization is not null)
        {
            Organization = await _db.Organizations
                .Include(o => o.Members)
                .ThenInclude(m => m.User)
                .SingleOrDefaultAsync(o => o.Id == organization.Id);
        }

        return Page();
    }
#endif

    async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var platformUserId = principal.GetPlatformUserId();
        return await GetUserQueryable().SingleOrDefaultAsync(u => u.PlatformUserId == platformUserId);
    }

    IQueryable<User> GetUserQueryable()
    {
        return _db.Users.Include(u => u.Members)
            .ThenInclude(m => m.Organization)
            .Include(u => u.Members)
            .ThenInclude(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role);
    }
}

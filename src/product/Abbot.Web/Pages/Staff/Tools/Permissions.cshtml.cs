using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Staff.Tools;

public class PermissionsPage : StaffToolsPage
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly AbbotContext _db;

    public PermissionsPage(
        IUserRepository userRepository,
        IRoleManager roleManager,
        AbbotContext db)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _db = db;
    }

    [BindProperty]
    public List<string>? Roles { get; set; }

    public MultiSelectList AvailableRoles { get; private set; } = null!;

    [BindProperty]
    public int? SkillId { get; set; }

    public List<SelectListItem> Skills { get; private set; } = null!;

    [BindProperty]
    public Capability Capability { get; set; }

    public List<Permission> Permissions { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await InitializePage();
        Roles = Viewer.MemberRoles.Select(mr => mr.Role.Name).ToList();
    }

    public async Task<IActionResult> OnPostSaveRolesAsync()
    {
        await InitializePage();
        var abbot = await _userRepository.EnsureAbbotMemberAsync(Viewer.Organization);
        var newRoles = (Roles ?? new()).Append(Security.Roles.Staff).ToList();
        await _roleManager.SyncRolesFromListAsync(Viewer, newRoles, abbot);
        _roleManager.SyncRolesToPrincipal(Viewer, User);
        StatusMessage = "Roles updated";
        await HttpContext.SignInAsync(User);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSavePermissionsAsync()
    {
        await InitializePage();
        if (SkillId is null)
        {
            StatusMessage = "No skill selected";
            return RedirectToPage();
        }

        var permission = await _db.Permissions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.MemberId == Viewer.Id && p.SkillId == SkillId.Value);

        if (permission is null)
        {
            permission = new Permission
            {
                Member = Viewer,
                SkillId = SkillId.Value,
                Creator = Viewer.User,
                Created = DateTimeOffset.UtcNow
            };

            await _db.Permissions.AddAsync(permission);
        }

        permission.Capability = Capability;
        await _db.SaveChangesAsync();
        StatusMessage = "Permissions updated";
        return RedirectToPage();
    }

    public async Task InitializePage()
    {
        AvailableRoles = new MultiSelectList(new[]
        {
            Security.Roles.Agent,
            Security.Roles.Administrator,
            // We're not going to allow removing from Staff here.
            // If you really need to do that, use /account/claims. LOL!
        });

        Skills = (await _db.Skills.ToListAsync())
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        Permissions = await _db.Permissions
            .Include(p => p.Skill) // Permissions for deleted skills not included.
            .Where(p => p.MemberId == Viewer.Id)
            .ToListAsync();
    }
}

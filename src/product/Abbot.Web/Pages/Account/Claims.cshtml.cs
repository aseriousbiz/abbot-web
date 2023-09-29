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
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Account;

public class ClaimsModel : AbbotPageModelBase
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly AbbotContext _db;

    public ClaimsModel(IUserRepository userRepository, IRoleManager roleManager, AbbotContext db)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _db = db;
    }

    [BindProperty]
    public string FormType { get; set; } = null!;

    [BindProperty]
    public List<string> Roles { get; set; } = new();

    public MultiSelectList AvailableRoles { get; private set; } = null!;

    [BindProperty]
    public int? SkillId { get; set; }

    public List<SelectListItem> Skills { get; private set; } = null!;

    [BindProperty]
    public Capability Capability { get; set; }

    public List<Permission> Permissions { get; private set; } = null!;

    public Member? CurrentMember { get; private set; }

#if DEBUG
    public async Task OnGetAsync()
    {
        await InitializePage();
        Roles = CurrentMember?.MemberRoles.Select(mr => mr.Role.Name).ToList() ?? new List<string>();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await InitializePage();
        if (CurrentMember is not null)
        {
            switch (FormType)
            {
                case "role":
                    {
                        var abbot = await _userRepository.EnsureAbbotMemberAsync(CurrentMember.Organization);
                        await _roleManager.SyncRolesFromListAsync(CurrentMember, Roles, abbot);
                        _roleManager.SyncRolesToPrincipal(CurrentMember, User);
                        StatusMessage = "Roles updated";
                        await HttpContext.SignInAsync(User);
                        break;
                    }
                case "permission" when SkillId is not null:
                    {
                        var permission = await _db.Permissions
                            .IgnoreQueryFilters()
                            .SingleOrDefaultAsync(
                                p => p.MemberId == CurrentMember.Id
                                     && p.SkillId == SkillId.Value);
                        if (permission is null)
                        {
                            permission = new Permission
                            {
                                Member = CurrentMember,
                                SkillId = SkillId.Value,
                                Creator = CurrentMember.User,
                                Created = DateTimeOffset.UtcNow
                            };
                            await _db.Permissions.AddAsync(permission);
                        }

                        permission.Capability = Capability;
                        await _db.SaveChangesAsync();
                        StatusMessage = "Permissions updated";
                        break;
                    }
            }
        }
        else
        {
            StatusMessage = "No Changes";
        }

        return RedirectToPage();
    }

    public async Task InitializePage()
    {
        CurrentMember = HttpContext.GetCurrentMember();

        AvailableRoles = new MultiSelectList(new[]
        {
            Security.Roles.Agent,
            Security.Roles.Administrator,
            Security.Roles.Staff
        });

        Skills = (await _db.Skills.ToListAsync())
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        Permissions = CurrentMember is not null
            ? await _db.Permissions
                .Include(p => p.Skill) // Permissions for deleted skills not included.
                .Where(p => p.MemberId == CurrentMember.Id)
                .ToListAsync()
            : new List<Permission>();
    }
#endif
    public bool IsDebug { get; }
#if DEBUG
        = true;
#endif
}

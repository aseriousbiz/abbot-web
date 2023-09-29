using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Settings.Organization.Users;

public class ArchiveUserPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IAuditLog _auditLog;

    public ArchiveUserPage(IUserRepository userRepository, IRoleManager roleManager, IAuditLog auditLog)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _auditLog = auditLog;
    }

    public Member Subject { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        var subject = await InitializeState(id);

        if (subject is null || subject.Id == Viewer.Id)
        {
            return NotFound();
        }

        Subject = subject;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? id)
    {
        var subject = await InitializeState(id);

        if (subject is null || subject.Id == Viewer.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var roles = Viewer.MemberRoles.Select(r => r.Role).ToList();

        foreach (var role in roles)
        {
            await _roleManager.RemoveUserFromRoleAsync(subject, role.Name, Viewer);
        }

        await _userRepository.ArchiveMemberAsync(subject, Viewer);
        await _auditLog.LogArchiveUserAsync(subject, Viewer.User);

        StatusMessage = "User archived";

        return RedirectToPage("/Settings/Organization/Users/Index");
    }

    async Task<Member?> InitializeState(string? id)
    {
        if (id is null)
        {
            return null;
        }

        var subject = await _userRepository.GetByPlatformUserIdAsync(id, Viewer.Organization);
        if (subject is null)
        {
            return null;
        }

        Subject = subject;
        return subject;
    }
}

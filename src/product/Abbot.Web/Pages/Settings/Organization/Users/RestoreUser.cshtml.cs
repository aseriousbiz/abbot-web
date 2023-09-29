using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Settings.Organization.Users;

public class RestoreUserPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;

    public RestoreUserPage(IUserRepository userRepository, IRoleManager roleManager, IAuditLog auditLog, IClock clock)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _auditLog = auditLog;
        _clock = clock;
    }

    public Member Subject { get; private set; } = null!;

    public bool HasEnoughPurchasedSeats { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        var subject = await InitializeState(id);

        if (subject is null)
        {
            return NotFound();
        }

        Subject = subject;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? id)
    {
        var subject = await InitializeState(id);

        if (subject is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (HasEnoughPurchasedSeats)
        {
            await _roleManager.RestoreMemberAsync(subject, Viewer);
            StatusMessage = "User restored and added to the Agent role.";
            await _auditLog.LogRestoreUserAsync(subject, Viewer.User);
        }
        else
        {
            // This generally shouldn't happen since we don't render the form if you don't have enough seats.
            // But just in case someone stays on the form and runs out of agents in the background.
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Not enough purchased seats to restore user.";
        }

        return RedirectToPage("Index");
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

        var agentCount = await _roleManager.GetCountInRoleAsync(Roles.Agent, Organization);

        HasEnoughPurchasedSeats = Organization.CanAddAgent(agentCount, _clock.UtcNow);

        Subject = subject;
        return subject;
    }
}

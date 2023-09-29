using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Slack;

namespace Serious.Abbot.Pages.Settings.Organization.Users;

public class PendingPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IBackgroundSlackClient _backgroundSlackClient;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;

    public PendingPage(
        IUserRepository userRepository,
        IRoleManager roleManager,
        IBackgroundSlackClient backgroundSlackClient,
        IAuditLog auditLog,
        IClock clock)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _backgroundSlackClient = backgroundSlackClient;
        _auditLog = auditLog;
        _clock = clock;
    }

    [BindProperty]
    public InputModel Input { get; init; } = new();

    public IPaginatedList<Member> PendingUsers { get; private set; } = null!;

    public string Platform { get; private set; } = null!;

    public bool HasEnoughPurchasedSeats { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? p = 1)
    {
        int pageNumber = p ?? 1;
        Platform = Organization.PlatformType.ToString();

        var pendingQueryable = _userRepository.GetPendingMembersQueryable(Organization)
            .OrderByDescending(u => u.Created);
        PendingUsers = await PaginatedList.CreateAsync(pendingQueryable, pageNumber, WebConstants.LongPageSize);

        var agentCount = await _roleManager.GetCountInRoleAsync(Roles.Agent, Organization);

        HasEnoughPurchasedSeats = Organization.CanAddAgent(agentCount, _clock.UtcNow);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            StatusMessage = "Something went wrong";
            return RedirectToPage();
        }

        var subject = await _userRepository.GetByPlatformUserIdAsync(Input.Id!, Organization);

        if (subject is null)
        {
            return NotFound();
        }

        var agentCount = await _roleManager.GetCountInRoleAsync(Roles.Agent, Organization);
        HasEnoughPurchasedSeats = Organization.CanAddAgent(agentCount, _clock.UtcNow);

        if (!HasEnoughPurchasedSeats)
        {
            // This generally shouldn't happen since we don't render the form if you don't have enough seats.
            // But just in case someone stays on the form and runs out of agents in the background.
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Not enough purchased seats to approve this user.";
            return RedirectToPage();
        }

        await _roleManager.AddUserToRoleAsync(subject, Roles.Agent, Viewer);

        var websiteHyperlink = new Hyperlink(new Uri($"https://{WebConstants.DefaultHost}"), WebConstants.DefaultHost);
        _backgroundSlackClient.EnqueueDirectMessages(
            Organization,
            new[] { subject },
            $":unlock: Access request for the `{subject.Organization.Name}` organization on {websiteHyperlink} is granted.");

        StatusMessage = "User approved. I sent them an email and DM to let them know they can access the site.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDenyAsync()
    {
        if (!ModelState.IsValid)
        {
            StatusMessage = "Model state not valid";
            return RedirectToPage();
        }

        var subject = await _userRepository.GetByPlatformUserIdAsync(Input.Id!, Viewer.Organization);

        if (subject is null)
        {
            return NotFound();
        }

        await _userRepository.ArchiveMemberAsync(subject, Viewer);
        await _auditLog.LogDenyUser(subject, Viewer.User);

        StatusMessage = "User denied.";

        return RedirectToPage();
    }

    public class InputModel
    {
        public string? Id { get; set; }
    }
}

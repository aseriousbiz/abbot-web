using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Users;

public class AssignPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IRoomRepository _roomRepository;
    readonly IClock _clock;

    public AssignPage(
        IUserRepository userRepository,
        IRoleManager roleManager,
        IRoomRepository roomRepository,
        IClock clock)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _roomRepository = roomRepository;
        _clock = clock;
    }

    public Plan Plan { get; private set; } = null!;

    public Member Subject { get; private set; } = null!;

    public IReadOnlyList<string> FirstResponderRooms { get; private set; } = null!;

    public bool HasEnoughPurchasedSeats { get; private set; }

    public IReadOnlyList<SelectListItem> Roles { get; private set; } = null!;

    public bool IsSelf { get; private set; }

    [BindProperty]
    public IReadOnlyList<string> SelectedRoles { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        var subject = await InitializePage(id);
        if (subject is null)
        {
            return NotFound();
        }

        Roles = await GetRolesSelectListItemsAsync(subject);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? id)
    {
        var subject = await InitializePage(id);
        if (subject is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (IsSelf && !SelectedRoles.Contains(Security.Roles.Administrator))
        {
            // This should never happen because we prevent it in the UI.
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}You cannot remove your own Administrator role.";
            return RedirectToPage();
        }

        bool addAgent = SelectedRoles.Contains(Security.Roles.Agent);

        if (!HasEnoughPurchasedSeats && addAgent && !subject.IsAgent())
        {
            // This generally shouldn't happen since we don't render the form if you don't have enough seats.
            // But just in case someone stays on the form and runs out of agents in the background.
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Not enough purchased seats to assign another Agent.";
            return RedirectToPage();
        }

        await _roleManager.SyncRolesFromListAsync(subject, SelectedRoles, Viewer);

        if (!addAgent)
        {

            // In order to have any room role assignments, you have to be a member of the `Agent` role.
            // So we're going to remove those assignments here.
            await _roomRepository.RemoveAllRoomAssignmentsForMemberAsync(subject);
        }

        StatusMessage = "Roles assignments updated";
        return RedirectToPage(new { id });
    }

    async Task<Member?> InitializePage(string? id)
    {
        if (id is null)
        {
            return null;
        }

        var subject = await _userRepository.GetByPlatformUserIdAsync(id, Organization);
        if (subject is null)
        {
            return null;
        }

        Subject = subject;

        IsSelf = subject.Id == Viewer.Id;

        FirstResponderRooms = (await _roomRepository.GetRoomAssignmentsAsync(subject))
            .Where(a => a.Role == RoomRole.FirstResponder)
            .Select(a => $"#{a.Room.Name ?? "unknown"}")
            .ToList();

        var agentCount = await _roleManager.GetCountInRoleAsync(Security.Roles.Agent, Organization);

        HasEnoughPurchasedSeats = Organization.CanAddAgent(agentCount, _clock.UtcNow);

        Plan = subject.Organization.GetPlan();

        return subject;
    }

    async Task<IReadOnlyList<SelectListItem>> GetRolesSelectListItemsAsync(Member subject)
    {
        var roles = (await _roleManager.GetRolesAsync())
            .Select(r => r.Name)
            .OrderByDescending(r => r)
            // We want Agent listed first. This'll do for now
            .Where(r => r != Security.Roles.Staff)
            .ToList();

        var subjectRoles = subject.MemberRoles.Select(mr => mr.Role.Name).ToList();

        bool ShouldBeDisabled(string role)
        {
            return role is Security.Roles.Agent && !HasEnoughPurchasedSeats && !subjectRoles.Contains(Security.Roles.Agent)
                   || role is Security.Roles.Administrator && IsSelf;
        }

        return roles
            .Select(role => new SelectListItem(
                text: role,
                value: role,
                selected: subjectRoles.Contains(role),
                disabled: ShouldBeDisabled(role)))
            .ToList();
    }
}

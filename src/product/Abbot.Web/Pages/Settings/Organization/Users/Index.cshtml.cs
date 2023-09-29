using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Settings.Organization.Users;

public class IndexPage : UserPage
{
    readonly IUserRepository _userRepository;

    public IndexPage(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Users", new { Id = Organization.PlatformId });

    public int WaitListCount { get; private set; }

    public string Platform { get; private set; } = null!;

    public IPaginatedList<Member> Members { get; private set; } = null!;

    public int PaidActiveMembers { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? p = 1)
    {
        int pageNumber = p ?? 1;
        Platform = Organization.PlatformType.ToString();

        var usersQueryable = _userRepository.GetActiveMembersQueryable(Organization);
        Members = await PaginatedList.CreateAsync(usersQueryable, pageNumber, WebConstants.LongPageSize);

        WaitListCount = await _userRepository.GetPendingMembersQueryable(Organization).CountAsync();

        PaidActiveMembers = Organization.PurchasedSeatCount;

        return Page();
    }
}

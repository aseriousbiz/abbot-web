using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Collections;

namespace Serious.Abbot.Pages.Settings.Organization.Users;

public class ArchivedPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IClock _clock;

    public ArchivedPage(IUserRepository userRepository, IRoleManager roleManager, IClock clock)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _clock = clock;
    }

    public string Platform { get; private set; } = null!;

    public IPaginatedList<Member> ArchivedUsers { get; private set; } = null!;

    public bool HasEnoughPurchasedSeats { get; private set; }

    public async Task OnGetAsync(int? p)
    {
        int pageNumber = p ?? 1;

        var archiveQueryable = _userRepository.GetArchivedMembersQueryable(Organization);

        ArchivedUsers = await PaginatedList.CreateAsync(archiveQueryable, pageNumber, WebConstants.LongPageSize);

        Platform = Organization.PlatformType.ToString();

        var agentCount = await _roleManager.GetCountInRoleAsync(Roles.Agent, Organization);
        HasEnoughPurchasedSeats = Organization.CanAddAgent(agentCount, _clock.UtcNow);

    }
}

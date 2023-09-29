using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

namespace Serious.Abbot.Pages;

public class AccessDeniedPage : PageModel
{
    readonly IUserRepository _userRepository;

    public AccessDeniedPage(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public bool AccessPending { get; set; }

    public async Task OnGetAsync()
    {
        var platformUserId = User.GetPlatformUserId();
        var organization = HttpContext.GetCurrentOrganization();

        if (platformUserId is null || organization is null)
        {
            AccessPending = false;
            return;
        }

        var user = await _userRepository.GetByPlatformUserIdAsync(platformUserId, organization);
        AccessPending = user is not null && !User.IsMember() && user.AccessRequestDate is not null;
    }
}

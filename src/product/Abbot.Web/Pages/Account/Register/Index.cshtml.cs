using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Scripting;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Pages.Account.Register;

public class IndexPage : PageModel
{
    static readonly ILogger<IndexPage> Log = ApplicationLoggerFactory.CreateLogger<IndexPage>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IUrlGenerator _urlGenerator;
    readonly IBackgroundSlackClient _backgroundSlackClient;

    public IndexPage(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IRoleManager roleManager,
        IUrlGenerator urlGenerator,
        IBackgroundSlackClient backgroundSlackClient)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _roleManager = roleManager;
        _urlGenerator = urlGenerator;
        _backgroundSlackClient = backgroundSlackClient;
    }

    public Organization Organization { get; private set; } = null!;

    public bool RequiresUserRegistration { get; private set; }

    public async Task OnGetAsync()
    {
        await InitializeProperties();
    }

    public async Task<RedirectToPageResult> OnPostAsync()
    {
        await InitializeProperties();

        // Updates the user so that they are in the Pending state.
        User.RemoveRegistrationStatusClaim();
        var member = await _userRepository.EnsureCurrentMemberWithRolesAsync(User, Organization);
        member.AccessRequestDate = DateTimeOffset.UtcNow;
        await _userRepository.UpdateUserAsync();

        var admins = await _roleManager.GetMembersInRoleAsync(Roles.Administrator, Organization);
        var waitListUrl = Url.PageLink("/Settings/Organization/Users/Pending");

        Log.SendingMemberAccessRequestToAdmins(
            string.Join(',', admins.Select(m => m.User.PlatformUserId)),
            member.Id,
            Organization.PlatformId,
            Organization.PlatformType);

        _backgroundSlackClient.EnqueueDirectMessages(
            Organization,
            admins,
            $":closed_lock_with_key: {member.ToMention()} requests access to the `{Organization.Name}` organization on <https://{WebConstants.DefaultHost}|{WebConstants.DefaultHost}>. Visit the <{waitListUrl}|Wait list page> to grant or deny access.");

        await HttpContext.SignInAsync(User);
        return RedirectToPage("/Status/AccessDenied");
    }

    async Task InitializeProperties()
    {
        Organization = (await _organizationRepository.EnsureAsync(User)).Entity;
        RequiresUserRegistration = User.GetRegistrationStatusClaim() is RegistrationStatus.ApprovalRequired;
    }
}
public static partial class RegisterIndexPageLoggingExtensions
{

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message =
            "Sending member requests access DM to admins ({AdminPlatformUserIds}, Member Id: {MemberId}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void SendingMemberAccessRequestToAdmins(
        this ILogger<IndexPage> logger,
        string adminPlatformUserIds,
        int memberId,
        string platformId,
        PlatformType platformType);
}

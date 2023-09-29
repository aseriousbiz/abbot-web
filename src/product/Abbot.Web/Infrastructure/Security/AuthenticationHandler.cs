using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.Security;

public class AuthenticationHandler : IAuthenticationHandler
{
    static readonly ILogger<AuthenticationHandler> Log = ApplicationLoggerFactory.CreateLogger<AuthenticationHandler>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly IBackgroundSlackClient _backgroundSlackClient;
    readonly IAnalyticsClient _analyticsClient;
    readonly IRoleManager _roleManager;
    readonly IPublishEndpoint _publishEndpoint;
    readonly IClock _clock;

    public AuthenticationHandler(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IBackgroundSlackClient backgroundSlackClient,
        IAnalyticsClient analyticsClient,
        IRoleManager roleManager,
        IPublishEndpoint publishEndpoint,
        IClock clock)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _backgroundSlackClient = backgroundSlackClient;
        _analyticsClient = analyticsClient;
        _roleManager = roleManager;
        _publishEndpoint = publishEndpoint;
        _clock = clock;
    }

    public async Task HandleAuthenticatedUserAsync(ClaimsPrincipal principal)
    {
        Log.MethodEntered(typeof(AuthenticationHandler), nameof(HandleAuthenticatedUserAsync), null);

        var (organization, _) = await _organizationRepository.EnsureAsync(principal);

        if (!organization.IsComplete())
        {
            Log.MethodEntered(typeof(AuthenticationHandler), nameof(HandleAuthenticatedUserAsync), "Organization not complete, updating it.");
            await _organizationRepository.UpdateOrganizationAsync(organization, principal);
            if (organization.PlatformType is PlatformType.Slack)
            {
                _backgroundSlackClient.EnqueueUpdateOrganization(organization);
            }
        }

        var abbotMember = await _organizationRepository.EnsureAbbotMember(organization);
        var member = await _userRepository.EnsureCurrentMemberWithRolesAsync(principal, organization);

        await _organizationRepository.EnsureActivatedAsync(organization, member);

        _roleManager.SyncRolesToPrincipal(member, principal);
        if (await ShouldAddUserToStaff(organization))
        {
            await _roleManager.AddUserToRoleAsync(member, Roles.Staff, abbotMember);
            principal.AddRoleClaim(Roles.Staff);
            await _roleManager.AddUserToRoleAsync(member, Roles.Administrator, abbotMember);
            principal.AddRoleClaim(Roles.Administrator);
            principal.RemoveRegistrationStatusClaim();
            principal.AddRegistrationStatusClaim(RegistrationStatus.Ok);
        }
        else if (await ShouldAddUserToAdmins(organization))
        {
            member.Welcomed = true; // We send an installation welcome message to the installing user.
            await _roleManager.AddUserToRoleAsync(member, Roles.Administrator, abbotMember);
            principal.AddRoleClaim(Roles.Administrator);
            principal.RemoveRegistrationStatusClaim();
            principal.AddRegistrationStatusClaim(RegistrationStatus.Ok);
        }
        else if (await ShouldAddUserToAgentsAsync(member, organization))
        {
            await _roleManager.AddUserToRoleAsync(member, Roles.Agent, abbotMember);
            principal.AddRoleClaim(Roles.Agent);
            // At this point, the bot should be fully installed.
        }
        else if (!principal.IsMember() && member.AccessRequestDate is null)
        {
            principal.AddRegistrationStatusClaim(RegistrationStatus.ApprovalRequired);
        }
    }

    async Task<bool> ShouldAddUserToStaff(Organization organization)
    {
        return organization.PlatformId == WebConstants.ASeriousBizSlackId
               && organization.PlatformType == PlatformType.Slack
               && !await _organizationRepository.ContainsAtLeastOneUserInRoleAsync(organization, Roles.Staff);
    }

    async Task<bool> ShouldAddUserToAdmins(Organization organization)
    {
        return organization.IsBotInstalled()
               && !await _organizationRepository.ContainsAtLeastOneUserInRoleAsync(organization, Roles.Administrator);
    }

    async Task<bool> ShouldAddUserToAgentsAsync(Member member, Organization organization)
    {
        if (member.CanManageConversations())
        {
            return false;
        }

        var agentCount = await _roleManager.GetCountInRoleAsync(Roles.Agent, organization);
        if (!organization.CanAddAgent(agentCount, _clock.UtcNow))
        {
            return false;
        }

        if (member.InvitationDate is not null && !member.IsAgent())
        {
            _analyticsClient.Track(
                "Invitation accepted",
                AnalyticsFeature.Invitations,
                member,
                organization);
            return true;
        }
        return organization.AutoApproveUsers;
    }

    public async Task HandleValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        // Discord users might not have a platform team id set yet.
        if (principal?.GetPlatformTeamId() is null)
            return;

        try
        {
            var member = await _userRepository.GetCurrentMemberAsync(principal);
            if (member is null)
            {
                context.RejectPrincipal();
                return;
            }

            context.HttpContext.SetCurrentMember(member);
            _roleManager.SyncRolesToPrincipal(member, principal);
            context.ShouldRenew = true;
        }
        catch (Exception ex)
        {
            // If we have an error fetching DB information, just reject the principal.
            // Suppressing the error means that anonymous endpoints aren't broken.
            context.RejectPrincipal();
            Log.ExceptionValidatingPrincipal(ex, principal.GetPlatformUserId(), principal.GetPlatformTeamId());
        }
    }
}

static partial class AuthenticationHandlerLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Exception validating principal for user {PlatformUserId} in {PlatformTeamId}")]
    public static partial void ExceptionValidatingPrincipal(
        this ILogger<AuthenticationHandler> logger,
        Exception ex,
         string? platformUserId,
        string? platformTeamId);
}

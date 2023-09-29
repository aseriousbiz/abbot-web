using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;

namespace Serious.Abbot.Security;

public static class ClaimsPrincipalExtensions
{
    static ClaimsIdentity GetClaimsIdentity(this IPrincipal principal)
    {
        var identity = principal.Identity
                       ?? throw new InvalidOperationException("The Identity for the Claims Principal is null!");
        return (ClaimsIdentity)identity;
    }

    public static bool IsAuthenticated(this ClaimsPrincipal principal)
    {
        return principal is { Identity: { IsAuthenticated: true } };
    }

    public static bool IsAdministrator(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Roles.Administrator);
    }

    public static bool IsAgent(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Roles.Agent);
    }

    public static bool CanManageConversations(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Roles.Agent) || principal.IsInRole(Roles.Administrator);
    }

    /// <summary>
    /// Returns the "Sub" claim. Since we use Auth0, this typically embeds multiple pieces of information.
    /// For example, for Slack it's in the format "oauth2|slack|TEAMID-USERID".
    /// </summary>
    /// <param name="claimsPrincipal">The authenticated user.</param>
    public static string? GetNameIdentifier(this ClaimsPrincipal claimsPrincipal)
    {
        var ret = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (ret is not null && ret.StartsWith("oauth2|slack-", StringComparison.InvariantCulture))
        {
            var parts = ret.Split('|');
            if (parts.Length == 3)
            {
                ret = $"{parts[0]}|slack|{parts[2]}";
            }
        }
        return ret;
    }

    /// <summary>
    /// When retrieving claims, this retrieves the `name` claim, which is the real name of the user, if known.
    /// </summary>
    /// <param name="claimsPrincipal">The <see cref="ClaimsPrincipal"/> of the authenticated user.</param>
    /// <remarks>
    /// For Slack this is the real name as far as we can tell.
    /// </remarks>
    public static string? GetName(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst(ClaimTypes.Name)?.Value;
    }

    public static string? GetEmail(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value;
    }

    public static string? GetAvatar(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst("picture")?.Value;
    }

    /// <summary>
    /// Returns the platform specific user Id for the authenticated user.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetPlatformUserId(this ClaimsPrincipal claimsPrincipal)
    {
        string claimName = $"{AbbotSchema.SchemaUri}platform_user_id";
        return claimsPrincipal.FindFirst(claimName)?.Value;
    }

    /// <summary>
    /// Returns the Team or Server Id for the organization that the authenticated user belongs to.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetPlatformTeamId(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}platform_id")?.Value;
    }

    /// <summary>
    /// Returns the Enterprise Id for the current user, if the user belongs to an Enterprise Grid.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetEnterpriseId(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}enterprise_id")?.Value;
    }

    /// <summary>
    /// Returns the Enterprise Domain for the current user, if the user belongs to an Enterprise Grid.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetEnterpriseDomain(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}enterprise_domain")?.Value;
    }

    /// <summary>
    /// Returns the Enterprise Name for the current user, if the user belongs to an Enterprise Grid.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetEnterpriseName(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}enterprise_name")?.Value;
    }

    /// <summary>
    /// Returns the name of the organization (Slack Team, Teams Team, or Discord Server) the authenticated
    /// user belongs to.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetPlatformTeamName(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}platform_name")?.Value;
    }

    /// <summary>
    /// Returns the domain for Slack Team the authenticated
    /// user belongs to.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetPlatformDomain(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}platform_domain")?.Value;
    }

    /// <summary>
    /// Returns the avatar for the Slack Team the authenticated
    /// user belongs to if available. For now, this probably only works for Slack.
    /// </summary>
    /// <remarks>
    /// This is set on authentication via the "Retrieve Chat Platform Info" Auth0 Rule.
    /// </remarks>
    /// <param name="claimsPrincipal">The authenticated user</param>
    public static string? GetPlatformAvatar(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirst($"{AbbotSchema.SchemaUri}platform_avatar")?.Value;
    }

    const string RegistrationStatus = nameof(RegistrationStatus);

    public static void AddRegistrationStatusClaim(this ClaimsPrincipal claimsPrincipal, RegistrationStatus status)
    {
        claimsPrincipal.AddClaim(RegistrationStatus, status.ToString());
    }

    public static void RemoveRegistrationStatusClaim(this ClaimsPrincipal claimsPrincipal)
    {
        var identity = claimsPrincipal.Identity
                       ?? throw new InvalidOperationException("The Identity for the Claims Principal is null!");
        var claimsIdentity = (ClaimsIdentity)identity;
        var claims = claimsIdentity.Claims.Where(c => c.Type == RegistrationStatus).ToList();
        foreach (var claim in claims)
        {
            claimsIdentity.RemoveClaim(claim);
        }
    }

    public static RegistrationStatus GetRegistrationStatusClaim(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal.FindFirst(RegistrationStatus)?.Value;
        if (claim is null)
        {
            return Security.RegistrationStatus.Ok;
        }

        return Enum.TryParse<RegistrationStatus>(claim, out var status)
            ? status
            : Security.RegistrationStatus.Ok;
    }

    public static void AddRoleClaim(this ClaimsPrincipal claimsPrincipal, string role)
    {
        claimsPrincipal.AddClaim(ClaimTypes.Role, role);
    }

    public static void RemoveRoleClaim(this ClaimsPrincipal claimsPrincipal, string role)
    {
        var identity = claimsPrincipal.GetClaimsIdentity();
        var existingRoleClaim = claimsPrincipal.GetRoleClaims().SingleOrDefault(r => r.Value == role);
        if (existingRoleClaim is not null)
        {
            identity.RemoveClaim(existingRoleClaim);
        }
    }

    static void AddClaim(this IPrincipal principal, string type, string value)
    {
        var identity = principal.GetClaimsIdentity();
        identity.AddClaim(new Claim(type, value));
    }

    public static IEnumerable<string> GetRoleClaimValues(this ClaimsPrincipal principal)
    {
        return GetRoleClaims(principal).Select(c => c.Value);
    }

    static IEnumerable<Claim> GetRoleClaims(this ClaimsPrincipal principal)
    {
        return principal
            .Claims
            .Where(c => c.Type == ClaimTypes.Role);
    }

    public static bool IsMember(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.Claims.Any(c => c.Type == ClaimTypes.Role);
    }
}

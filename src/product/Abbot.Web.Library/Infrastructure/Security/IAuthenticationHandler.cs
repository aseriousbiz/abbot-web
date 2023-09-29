using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Class used to handle authenticated users.
/// </summary>
public interface IAuthenticationHandler
{
    /// <summary>
    /// Handles role assignment and determines registration status for authenticated user.
    /// </summary>
    /// <param name="principal">The authenticated user.</param>
    Task HandleAuthenticatedUserAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Handles refreshing user claims, if necessary.
    /// </summary>
    Task HandleValidatePrincipalAsync(CookieValidatePrincipalContext context);
}

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Infrastructure.Security;
using Serious.AspNetCore;

namespace Serious.Abbot.Controllers;

[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
[AbbotWebHost]
public class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public async Task Login(string? returnUrl = "/", string? teamid = null, string? signature = null)
    {
        if (teamid is not null && signature is not null)
        {
            HttpContext.Response.Redirect($"/Account/Install/Complete?teamId={teamid}");
            return;
        }

        var properties = new AuthenticationProperties { RedirectUri = returnUrl };

        await HttpContext.ChallengeAsync("Auth0", properties);
    }

    [HttpGet("logout")]
    public async Task Logout()
    {
        await HttpContext.SignOutAsync("Auth0", new AuthenticationProperties
        {
            // Indicate here where Auth0 should redirect the user after a logout.
            // Note that the resulting absolute Uri must be whitelisted in the
            // **Allowed Logout URLs** settings for the app.
            RedirectUri = Request.IsLocal()
                ? "https://localhost:4979/"
                : Request.Host.Host.Equals("app.ab.bot", StringComparison.Ordinal)
                    ? "https://ab.bot/" // This should be config somewhere, but we can clean that up later.
                    : Url.Page("/Index")
        });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}

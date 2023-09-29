using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;

namespace Serious.Abbot.Controllers.InternalApi;

[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
[Route("api/internal/installation")]
public class InstallationController : InternalApiControllerBase
{
    readonly PlatformRequirements _platformRequirements;

    public InstallationController(PlatformRequirements platformRequirements)
    {
        _platformRequirements = platformRequirements;
    }

    /// <summary>
    /// Retrieves whether or not the bot is fully installed. This is called by the /Account/Install/Complete
    /// page in order to determine whether the installation is complete and the user can be redirected over
    /// to the Admin settings page.
    /// </summary>
    /// <param name="platformId">The platform id for the organization.</param>
    [HttpGet]
    [ProducesResponseType(typeof(InstallationResult), StatusCodes.Status200OK)]
    public IActionResult Get(string platformId)
    {
        if (!Organization.PlatformId.Equals(platformId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        return new JsonResult(new InstallationResult(
            Organization.IsBotInstalled() && _platformRequirements.HasRequiredScopes(Organization)));
    }
}

public record InstallationResult(bool BotInstalled);

using System;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Filters;
using Serious.Logging;
using IAuthenticationHandler = Serious.Abbot.Infrastructure.Security.IAuthenticationHandler;

namespace Serious.Abbot.Pages.Account.Install;

public class CompleteModel : UserPage
{
    static readonly ILogger<CompleteModel> Log = ApplicationLoggerFactory.CreateLogger<CompleteModel>();

    readonly IAuthenticationHandler _authenticationHandler;
    readonly PlatformRequirements _platformRequirements;

    public CompleteModel(
        IAuthenticationHandler authenticationHandler,
        PlatformRequirements platformRequirements)
    {
        _authenticationHandler = authenticationHandler;
        _platformRequirements = platformRequirements;
    }

    public string PlatformId { get; private set; } = null!;

    public string Platform { get; private set; } = string.Empty;

    public bool TeamIdMatchesOrganization { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? teamId)
    {
        Log.ReturnedFromInstallingAbbot(teamId);

        if (teamId is null)
        {
            return NotFound();
        }

        var organization = Organization;

        PlatformId = organization.PlatformId;

        Platform = organization.PlatformType.Humanize();

        TeamIdMatchesOrganization = organization.PlatformId.Equals(teamId, StringComparison.Ordinal);

        if (TeamIdMatchesOrganization && organization.IsBotInstalled() && _platformRequirements.HasRequiredScopes(organization))
        {
            await _authenticationHandler.HandleAuthenticatedUserAsync(User);
            await HttpContext.SignInAsync(User);

            return RedirectToPage(OrganizationStateFilter.OverviewPage);
        }

        return Page();
    }

    public Task<IActionResult> OnPostAsync(string? teamId)
    {
        return OnGetAsync(teamId);
    }
}

static partial class CompleteModelLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Returned from installing Abbot: (teamId: {TeamId})")]
    public static partial void ReturnedFromInstallingAbbot(this ILogger<CompleteModel> logger, string? teamId);
}

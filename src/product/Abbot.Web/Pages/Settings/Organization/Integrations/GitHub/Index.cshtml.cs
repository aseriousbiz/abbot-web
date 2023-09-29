using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Octokit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.GitHub;

public class IndexModel : SingleIntegrationPageBase<GitHubSettings>
{
    static readonly ILogger<IndexModel> Log = ApplicationLoggerFactory.CreateLogger<IndexModel>();
    readonly IGitHubClientFactory _gitHubClientFactory;

    public IndexModel(IIntegrationRepository integrationRepository, IGitHubClientFactory gitHubClientFactory) : base(integrationRepository)
    {
        _gitHubClientFactory = gitHubClientFactory;
    }

    public async Task OnGetAsync()
    {
        DefaultRepository = Settings.DefaultRepository;

        if (!IsInstalled)
            return;

        try
        {
            var client = await _gitHubClientFactory.CreateInstallationClientAsync(Integration, Settings);
            var repos = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();
            AvailableRepositories = repos.Repositories
                .Select(r => new SelectListItem(r.FullName, r.FullName) { Disabled = !r.HasIssues })
                .ToList();
        }
        catch (ApiException ex)
        {
            Log.ErrorCallingGitHubApi(ex, Settings.InstallationId);
        }
    }

    public IEnumerable<SelectListItem> AvailableRepositories { get; set; } = Array.Empty<SelectListItem>();

    public bool IsInstalled => Settings is { HasApiCredentials: true };

    public bool IsConfigured => false;

    [BindProperty]
    public string? DefaultRepository { get; set; }

    public async Task<IActionResult> OnPostDefaultRepository()
    {
        Settings.DefaultRepository = DefaultRepository;
        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
        return RedirectWithStatusMessage("Default repository saved.");
    }

    public async Task<IActionResult> OnPostUninstallAsync()
    {
        if (Settings.InstallationId == null)
        {
            StatusMessage = "The integration is not installed.";
            return RedirectToPage();
        }

        try
        {
            var github = _gitHubClientFactory.CreateAppClient();
            var status = await github.Connection.Delete(new Uri(github.Connection.BaseAddress, $"/app/installations/{Settings.InstallationId.Value}"));

            // We only get a status code back.
            if (status == HttpStatusCode.NoContent)
            {
                Settings.InstallationId = null;
                Settings.InstallationToken = null;
                Settings.InstallationTokenExpiryUtc = null;
                await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
                StatusMessage = "GitHub integration uninstalled.";
            }
            else
            {
                Log.FailedToUninstallGitHubApp(Settings.InstallationId.Value);
                StatusMessage = "Failed to uninstall GitHub integration.";
            }
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            // 404 means it's already been deleted; our settings should reflect that
            Settings.InstallationId = null;
            Settings.InstallationToken = null;
            Settings.InstallationTokenExpiryUtc = null;
            await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
            StatusMessage = "GitHub integration was already uninstalled.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        if (Integration is { Enabled: true })
        {
            await IntegrationRepository.DisableAsync(Organization, IntegrationType.GitHub, Viewer);
        }

        return RedirectWithStatusMessage("The GitHub integration has been disabled.");
    }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        if (Integration is not { Enabled: true })
        {
            await IntegrationRepository.EnableAsync(Organization, IntegrationType.GitHub, Viewer);
        }

        // It was already enabled, just stay on this page.
        // This means someone clicked the button twice, or synthesized a POST.
        return RedirectWithStatusMessage("The GitHub integration has been enabled.");
    }
}

public static partial class GitHubIndexPageLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Failed to uninstall GitHub App Installation {InstallationId}")]
    public static partial void FailedToUninstallGitHubApp(this ILogger<IndexModel> logger, int installationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Failed to call GitHub API for GitHub App Installation {InstallationId}")]
    public static partial void ErrorCallingGitHubApi(this ILogger<IndexModel> logger, Exception ex, int? installationId);
}

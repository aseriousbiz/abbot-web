using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Staff;

public class SettingsModel : StaffToolsPage
{
    public const string ConversationRefreshIntervalSecondsKey = "conversation-refresh-interval-seconds";

    readonly ISettingsManager _settingsManager;

    public DomId SettingsListDomId => new("settings-list");

    [BindProperty]
    public int ConversationRefreshIntervalSeconds { get; set; }

    public SettingsModel(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public async Task OnGetAsync()
    {
        var refreshInterval = await _settingsManager.GetAsync(SettingsScope.Global, ConversationRefreshIntervalSecondsKey);
        const int defaultRefreshIntervalSeconds = 15;
        if (refreshInterval is not null)
        {
            ConversationRefreshIntervalSeconds = int.TryParse(refreshInterval.Value, out var interval)
                ? interval
                : defaultRefreshIntervalSeconds;
        }
        else
        {
            ConversationRefreshIntervalSeconds = defaultRefreshIntervalSeconds;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ConversationRefreshIntervalSeconds is > 0 and < 2)
        {
            return TurboFlash("Refresh interval must be at least 2 seconds.", isError: true);
        }
        await _settingsManager.SetAsync(
            SettingsScope.Global,
            ConversationRefreshIntervalSecondsKey,
            $"{ConversationRefreshIntervalSeconds}",
            Viewer.User);
        return TurboFlash("Setting saved");
    }

    static SettingsScope Scope() => SettingsScope.Global;

    public async Task<IActionResult> OnPostSettingsAsync()
    {
        var settings = await _settingsManager.GetAllAsync(Scope());
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    public async Task<IActionResult> OnPostSettingDeleteAsync(string name)
    {
        var scope = Scope();
        if (await _settingsManager.GetAsync(scope, name) is not { } setting)
        {
            return TurboFlash($"Setting '{name}' not found.");
        }

        await _settingsManager.RemoveWithAuditingAsync(scope, name, Viewer.User, Viewer.Organization);

        var settings = await _settingsManager.GetAllAsync(scope);
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }
}

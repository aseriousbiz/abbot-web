using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.SlackApp;

[PageFeatureGate(FeatureFlags.SlackApp)]
public class SlackAppPageBase : SingleIntegrationPageBase<SlackAppSettings>
{
    public SlackAppPageBase(IIntegrationRepository integrationRepository)
        : base(integrationRepository)
    {
    }

    protected string NextSetupPage() =>
        this switch
        {
            { HasManifest: false } => "/Settings/Organization/Integrations/SlackApp/Manifest",
            { HasCredentials: false } => "/Settings/Organization/Integrations/SlackApp/Credentials",
            _ => "/Settings/Organization/Integrations/SlackApp/Index",
        };

    public bool HasManifest =>
        Settings is { HasManifest: true };

    public bool HasCredentials => Settings.HasCredentials(Integration);

    public bool IsInstalled => Settings.HasAuthorization(Integration);

    public bool CanInstall => HasManifest && HasCredentials;

    public string? CustomAppName =>
        Settings.Authorization?.AppName
        ?? Settings.Manifest?.AppName;

    public string? CustomBotName =>
        Settings.Authorization?.BotName
        ?? Settings.Manifest?.BotUserDisplayName;

    public string? SlackAppUrl => SlackAppSettings.SlackAppUrl(Integration);
}

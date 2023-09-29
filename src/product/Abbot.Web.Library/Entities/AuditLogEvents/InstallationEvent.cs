using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when an admin installs or uninstalls an app.
/// </summary>
public class InstallationEvent : AdminAuditEvent
{
    public override bool HasDetails => SerializedProperties is not null;
}

/// <summary>
/// Information about the platform installation event.
/// </summary>
/// <param name="Action">The action taken.</param>
/// <param name="PlatformType">The platform type.</param>
/// <param name="AppId">The platform App ID.</param>
/// <param name="AppName">The platform App Name, e.g. Abbot.</param>
/// <param name="BotUserId">The bot's platform User ID.</param>
/// <param name="BotName">The bot's name.</param>
/// <param name="Scopes">The app scopes.</param>
public record InstallationInfo(
    InstallationEventAction Action,
    PlatformType PlatformType,
    string? AppId,
    string? AppName,
    string? BotUserId,
    string? BotName,
    string? Scopes)
{
    public static InstallationInfo Create(InstallationEventAction action, Organization organization) =>
        new(action,
            organization.PlatformType,
            organization.BotAppId,
            organization.BotAppName,
            organization.PlatformBotUserId,
            organization.BotName,
            organization.Scopes);

    public static InstallationInfo Create(InstallationEventAction action, SlackAuthorization auth) =>
        new(action,
            PlatformType.Slack,
            auth.AppId,
            auth.AppName,
            auth.BotUserId,
            auth.BotName,
            auth.Scopes);
}

/// <summary>
/// The <see cref="InstallationEvent"/> action.
/// </summary>
public enum InstallationEventAction
{
    [Display(Name = "")]
    Unknown,

    Install,
    Uninstall,
}

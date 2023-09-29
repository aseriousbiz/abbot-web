using Serious.Abbot.Models;

namespace Serious.Abbot.Pages.Settings.Organization.Advanced;

/// <summary>
/// View model for the _BotShortcutSetting partial view
/// </summary>
public interface IBotShortcutViewModel
{
    Entities.Organization Organization { get; }
    BotShortcutInputModel Input { get; }

}

public record BotShortcutInputModel(char ShortcutCharacter, bool UserSkillEnabled);

/// <summary>
/// View model for the _OrgAvatarSetting partial view
/// </summary>
public interface IOrganizationAvatarViewModel
{
    public Entities.Organization Organization { get; }

    public FileInputModel OrgAvatarInput { get; }
}

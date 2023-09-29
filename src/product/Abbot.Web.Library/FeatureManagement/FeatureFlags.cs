namespace Serious.Abbot.FeatureManagement;

/// <summary>
/// Represents a feature flag which can be enabled/disabled for a user or organization.
/// </summary>
public static class FeatureFlags
{
    // Constants are burned in to the consuming assembly, so it's generally good practice _not_ to use something like nameof, which can change when a field is renamed.
    // These values are truly constant, once they're established, they shouldn't ever be changed.
    // This isn't a huge deal for a mostly-private library, but it's a harmless extra bit of caution.

    public const string SlackApp = "SlackApp";
    public const string GitHub = "GitHub";
    public const string Hubs = "Hubs";
    public const string MergeTicketing = "MergeTicketing";
    public const string ConversationListAutoRefresh = "ConversationListAutoRefresh";
    public const string AIEnhancements = "AIEnhancements";
    public const string AISkillPrompts = "AISkillPrompts";
    public const string AIConversationMatching = "AIConversationMatching";
    public const string MagicResponder = "MagicResponder";
    public const string Tasks = "Tasks";
    public const string Playbooks = "Playbooks";
    public const string PlaybookDispatching = "PlaybookDispatching";
    public const string PlaybookStepsWave1 = "PlaybookStepsWave1"; // The next wave of playbook steps.
    public const string PlaybookUpcomingEvents = "PlaybookUpcomingEvents"; // The next wave of playbook steps.

    public static readonly string FeatureManagerPortalUrl =
        "https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/abbot-global/providers/Microsoft.AppConfiguration/configurationStores/abbot-configuration/ff";
}

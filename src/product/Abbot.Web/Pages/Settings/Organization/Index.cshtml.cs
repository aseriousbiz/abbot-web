using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serious.Abbot.Clients;
using Serious.Abbot.Extensions;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Slack;
using Serious.Slack.AspNetCore;

namespace Serious.Abbot.Pages.Settings.Organization;

public class IndexPage : AdminPage
{
    readonly IIntegrationRepository _integrationRepository;
    readonly ISettingsManager _settingsManager;
    readonly IAbbotWebFileStorage _abbotWebFileStorage;
    readonly ISlackApiClient _slackApiClient;
    readonly SlackOptions _slackOptions;

    public IndexPage(
        IOrganizationRepository organizationRepository,
        IIntegrationRepository integrationRepository,
        ISettingsManager settingsManager,
        IAbbotWebFileStorage abbotWebFileStorage,
        ISlackApiClient slackApiClient,
        IOptions<SlackOptions> slackOptions,
        IAuditLog auditLog) : base(organizationRepository, auditLog)
    {
        _integrationRepository = integrationRepository;
        _settingsManager = settingsManager;
        _abbotWebFileStorage = abbotWebFileStorage;
        _slackApiClient = slackApiClient;
        _slackOptions = slackOptions.Value;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Status", new { Id = Organization.PlatformId });

    [BindProperty]
    public InputModel Input { get; set; } = new(false, false, false, false, false, false);

    public async Task OnGetAsync()
    {
        if (Organization is { ApiToken: { } })
        {
            await _slackApiClient.EnsureBotNameAndAvatar(Organization);
            await OrganizationRepository.SaveChangesAsync();
        }

        var allowEmojiResponseSetting = await GetReactionResponsesSetting();
        var allowTicketEmojiSetting = await ReactionHandler.GetAllowTicketReactionSetting(
            _settingsManager,
            Organization);

        var settings = Organization.Settings;
        var allowAiEnhancements = settings.AIEnhancementsEnabled is true;
        var ignoreSocialMessages = settings.IgnoreSocialMessages is true;
        var notifyOnNewConversationsOnly = settings.NotifyOnNewConversationsOnly;

        Input = new InputModel(
            Organization.AutoApproveUsers,
            allowEmojiResponseSetting,
            allowTicketEmojiSetting,
            allowAiEnhancements,
            ignoreSocialMessages,
            notifyOnNewConversationsOnly);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        bool autoApproveChanged = Input.AutoApproveUsers != Organization.AutoApproveUsers;

        if (autoApproveChanged)
        {
            Organization.AutoApproveUsers = Input.AutoApproveUsers;
            await AuditLog.LogAutoApproveChanged(Viewer.User, Organization);
            StatusMessage = "Auto approve users " + (Input.AutoApproveUsers
                ? "enabled"
                : "disabled");
            await OrganizationRepository.SaveChangesAsync();
        }
        else
        {
            StatusMessage = "No changes made.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConversationSettingsAsync()
    {
        Organization.Settings = Organization.Settings with
        {
            NotifyOnNewConversationsOnly = Input.NotifyOnNewConversationsOnly
        };

        await OrganizationRepository.SaveChangesAsync();

        var allowEmojiResponseSetting = await GetReactionResponsesSetting();
        var allowTicketEmojiSetting = await ReactionHandler.GetAllowTicketReactionSetting(
            _settingsManager,
            Organization);
        bool reactionResponseSettingChanged = allowEmojiResponseSetting != Input.AllowReactionResponses;
        bool ticketReactionSettingChanged = allowTicketEmojiSetting != Input.AllowTicketReactions;

        if (ticketReactionSettingChanged)
        {
            await ReactionHandler.SetAllowTicketReactionSetting(_settingsManager, Input.AllowTicketReactions, Viewer.User, Organization);

            var (eventName, description) = Input.AllowTicketReactions
                ? ("Enabled", "Enabled the 🎫 reaction for the organization by default.")
                : ("Disabled", "Disabled the 🎫 reaction for the organization by default.");

            await AuditLog.LogAuditEventAsync(
                new()
                {
                    Type = new("Organization.Reactions.Ticket", eventName),
                    Actor = Viewer,
                    Organization = Organization,
                    Description = description
                });
        }

        await OrganizationRepository.SetAISettingsWithAuditing(
                Input.AllowAIEnhancements,
                Input.IgnoreSocialMessages,
                Organization,
                Viewer);

        if (reactionResponseSettingChanged)
        {
            await SetReactionResponsesSetting(Input.AllowReactionResponses);
        }

        StatusMessage = "Conversation settings saved.";

        return RedirectToPage();
    }

    async Task<bool> GetReactionResponsesSetting() => await ReactionHandler.GetAllowReactionResponsesSetting(_settingsManager, Organization);

    async Task SetReactionResponsesSetting(bool value) =>
        await ReactionHandler.SetAllowReactionResponsesSetting(_settingsManager, value, Viewer.User, Organization);

    public record InputModel(
        bool AutoApproveUsers,
        bool AllowReactionResponses,
        bool AllowTicketReactions,
        bool AllowAIEnhancements,
        bool IgnoreSocialMessages,
        bool NotifyOnNewConversationsOnly);
}

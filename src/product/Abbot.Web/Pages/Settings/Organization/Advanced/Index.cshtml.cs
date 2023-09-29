using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Clients;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Settings.Organization.Advanced;

public class IndexPage : AdminPage, IBotShortcutViewModel, IOrganizationAvatarViewModel
{
    readonly IAbbotWebFileStorage _abbotWebFileStorage;

    public IndexPage(
        IOrganizationRepository organizationRepository,
        IAbbotWebFileStorage abbotWebFileStorage,
        IAuditLog auditLog)
        : base(organizationRepository, auditLog)
    {
        _abbotWebFileStorage = abbotWebFileStorage;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Status", new { Id = Organization.PlatformId });

    [BindProperty]
    public BotShortcutInputModel Input { get; set; } = null!;

    [BindProperty]
    public bool FallbackChatResponderEnabled { get; set; }

    [BindProperty]
    public bool ApiEnabled { get; set; }

    [BindProperty]
    public FileInputModel OrgAvatarInput { get; set; } = new();

    public void OnGet()
    {
        Input = new BotShortcutInputModel(Organization.ShortcutCharacter, Organization.UserSkillsEnabled);

        OrgAvatarInput.Prefix = "org-avatar";
        OrgAvatarInput.SavePageHandler = "SaveOrgAvatar";
        OrgAvatarInput.Url = Organization.Avatar;

        ApiEnabled = Organization.ApiEnabled;
        FallbackChatResponderEnabled = Organization.FallbackResponderEnabled;
    }

    public async Task<IActionResult> OnPostSaveShortcutSettingAsync()
    {
        var oldShortcut = Organization.ShortcutCharacter;
        Organization.ShortcutCharacter = Input.ShortcutCharacter;
        await AuditLog.LogOrganizationShortcutChanged(Viewer.User, oldShortcut, Organization);
        StatusMessage = "Shortcut character updated";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveCommandLineApiSettingAsync()
    {
        if (ApiEnabled != Organization.ApiEnabled)
        {
            Organization.ApiEnabled = ApiEnabled;
            await AuditLog.LogApiEnabledChanged(Viewer.User, Organization);
            var action = ApiEnabled
                ? "enabled"
                : "disabled";

            StatusMessage = $"API {action}";

            await OrganizationRepository.SaveChangesAsync();
        }
        else
        {
            StatusMessage = "No changes made";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveFallbackSettingAsync()
    {
        if (FallbackChatResponderEnabled != Organization.FallbackResponderEnabled)
        {
            Organization.FallbackResponderEnabled = FallbackChatResponderEnabled;
            await AuditLog.LogDefaultChatResponderEnabledChanged(Viewer.User, Organization);
            await OrganizationRepository.SaveChangesAsync();
            StatusMessage = "Fallback chat responder setting updated";

        }
        else
        {
            StatusMessage = "No changes made";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveOrgAvatarAsync()
    {
        return await SaveAvatarAsync(OrgAvatarInput, "Organization", (avatar) => Organization.Avatar = avatar);
    }

    // This happens via an Ajax result, hence no StatusMessage and redirect.
    public async Task<IActionResult> OnPostUploadOrgAvatarAsync()
    {
        return await UploadAvatarAsync(OrgAvatarInput.File, _abbotWebFileStorage.UploadOrganizationAvatarAsync);
    }

    public async Task<IActionResult> OnPostSaveUserSkillEnabledAsync()
    {
        Organization.UserSkillsEnabled = Input.UserSkillEnabled;
        await OrganizationRepository.SaveChangesAsync();
        await AuditLog.LogAdminActivityAsync(
            $"{(Input.UserSkillEnabled ? "Enabled" : "Disabled")} custom skill execution.",
            Viewer.User,
            Organization);
        StatusMessage = "Custom skills are now " + (Input.UserSkillEnabled ? "enabled" : "disabled");
        return RedirectToPage();
    }
}

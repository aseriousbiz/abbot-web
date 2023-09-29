using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Integrations;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore.Turbo;
using Serious.Collections;
using Serious.Slack;
using Member = Serious.Abbot.Entities.Member;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class MemberPage : OrganizationDetailPage
{
    readonly IRoleManager _roleManager;
    readonly ISettingsManager _settingsManager;
    readonly ILinkedIdentityRepository _linkedIdentityRepository;
    readonly IEnumerable<IUserIdentityLinker> _userIdentityLinkers;
    readonly ISlackApiClient _slackApiClient;

    public Member SubjectMember { get; set; } = null!;

    public DomId UserInfoDomId { get; } = new("user-info");

    public DomId ChannelMembershipsDomId { get; } = new("channel-memberships");

    public DomId SettingsListDomId { get; } = new("settings-list");

    public DomId ExternalIdentitiesDomId { get; } = new("external-identities");

    public IPaginatedList<Member> Memberships { get; set; } = null!;

    public MemberPage(
        AbbotContext db,
        IAuditLog auditLog,
        IRoleManager roleManager,
        ISettingsManager settingsManager,
        ILinkedIdentityRepository linkedIdentityRepository,
        IEnumerable<IUserIdentityLinker> userIdentityLinkers,
        ISlackApiClient slackApiClient)
        : base(db, auditLog)
    {
        _roleManager = roleManager;
        _settingsManager = settingsManager;
        _linkedIdentityRepository = linkedIdentityRepository;
        _userIdentityLinkers = userIdentityLinkers;
        _slackApiClient = slackApiClient;
    }

    public async Task<IActionResult> OnGetAsync(string orgId, string id)
    {
        return await LoadMemberAsync(orgId, id) ?? Page();
    }

    public async Task<IActionResult> OnPostExternalIdentities(string orgId, string id)
    {
        if (await LoadMemberAsync(orgId, id) is { } result)
        {
            return result;
        }

        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("ExternalIdentities", "Viewed"),
                Actor = Viewer,
                Organization = Organization,
                Description = "Staff member viewed user's External Identities",
                StaffPerformed = true,
                StaffOnly = true,
            });

        var identities = await _linkedIdentityRepository.GetAllLinkedIdentitiesForMemberAsync(SubjectMember);
        return TurboUpdate(ExternalIdentitiesDomId, Partial("_IdentitiesList", identities));
    }

    public async Task<IActionResult> OnPostEditExternalIdentity(string orgId, string id, int? identityId, string? externalId)
    {
        if (await LoadMemberAsync(orgId, id) is { } result)
        {
            return result;
        }

        TurboStreamViewResult? flash;

        var identities = await _linkedIdentityRepository.GetAllLinkedIdentitiesForMemberAsync(SubjectMember);
        var identity = identities.FirstOrDefault(i => i.Id == identityId);
        if (identity is null)
        {
            flash = TurboFlash("Identity not found.", isError: true);
        }
        else if (externalId is { Length: > 0 })
        {
            var oldProperties = new {
                identity.ExternalId,
                identity.ExternalName,
                identity.ExternalMetadata,
            };

            identity.ExternalId = externalId;
            identity.ExternalMetadata = null;
            identity.ExternalName = null;
            await _linkedIdentityRepository.UpdateLinkedIdentityAsync(identity);

            var linker = _userIdentityLinkers.FirstOrDefault(l => l.Type == identity.Type);
            if (linker is not null)
            {
                await linker.ResolveIdentityAsync(identity.Organization, identity.Member);
            }

            await AuditLog.LogAuditEventAsync(
                new()
                {
                    Type = new("ExternalIdentities", "Updated"),
                    Actor = Viewer,
                    Organization = Organization,
                    Description = $"Staff member updated user's {identity.Type.Humanize()} External Identity",
                    EntityId = identityId,
                    Properties = oldProperties,
                    StaffPerformed = true,
                    StaffOnly = false,
                });

            flash = TurboFlash($"{identity.Type.Humanize()} ExternalId updated.");
        }
        else
        {
            var oldProperties = new {
                identity.ExternalId,
                identity.ExternalName,
                identity.ExternalMetadata,
            };

            await _linkedIdentityRepository.RemoveIdentityAsync(identity);

            await AuditLog.LogAuditEventAsync(
                new()
                {
                    Type = new("ExternalIdentities", "Removed"),
                    Actor = Viewer,
                    Organization = Organization,
                    Description = $"Staff member removed user's {identity.Type.Humanize()} External Identity",
                    EntityId = identityId,
                    Properties = oldProperties,
                    StaffPerformed = true,
                    StaffOnly = false,
                });

            flash = TurboFlash($"{identity.Type.Humanize()} link removed.");
        }

        return TurboStream(
            flash,
            TurboUpdate(ExternalIdentitiesDomId, Partial("_IdentitiesList", identities)));
    }

    public SettingsScope Scope() => SettingsScope.Member(SubjectMember);

    public async Task<IActionResult> OnPostSettingsAsync(string orgId, string id)
    {
        if (await LoadMemberAsync(orgId, id) is { } result)
        {
            return result;
        }

        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("User.Settings", "Viewed"),
                Actor = Viewer,
                Organization = Organization,
                Description = "Staff member viewed user's Settings",
                StaffPerformed = true,
                StaffOnly = true,
            });

        var settings = await _settingsManager.GetAllAsync(Scope());
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    public async Task<IActionResult> OnPostSettingDeleteAsync(string orgId, string id, string name)
    {
        if (await LoadMemberAsync(orgId, id) is { } result)
        {
            return result;
        }

        var scope = Scope();
        if (await _settingsManager.GetAsync(scope, name) is not { } setting)
        {
            return TurboFlash($"Setting '{name}' not found.");
        }

        await _settingsManager.RemoveWithAuditingAsync(scope, name, Viewer.User, Organization);

        var settings = await _settingsManager.GetAllAsync(scope);
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    public async Task<IActionResult> OnPostUserInfoAsync(string orgId, string id)
    {
        if (await LoadMemberAsync(orgId, id) is { } result)
        {
            return result;
        }

        var apiToken = SubjectMember.Organization.ApiToken?.Reveal();
        if (apiToken is null)
        {
            return TurboUpdate(UserInfoDomId,
                """
                <div class="hard-box mt-5">
                    <strong class="p-3">Unknown Api Token</strong>
                    <pre>Subject Member's org does not have API Token.</pre>
                </div>
                """);
        }

        var response = await _slackApiClient.GetUserInfo(
            apiToken,
            user: SubjectMember.User.PlatformUserId);

        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("User.SlackUserInfo", "Viewed"),
                Actor = Viewer,
                Organization = Organization,
                Description = "Staff member viewed `users.info` Slack API response",
                StaffPerformed = true,
                StaffOnly = true,
            });

        var json = JsonConvert.SerializeObject(response, Formatting.Indented);
        return TurboUpdate(UserInfoDomId,
            $"""
            <div class="hard-box mt-5">
                <pre>{WebUtility.HtmlEncode(json)}</pre>
            </div>
            """);
    }

    public async Task<IActionResult> OnPostChannelMembershipsAsync(string orgId, string id)
    {
        if (await LoadMemberAsync(orgId, id) is { } result)
        {
            return result;
        }

        var apiToken = SubjectMember.Organization.ApiToken?.Reveal();
        if (apiToken is null)
        {
            return TurboUpdate(ChannelMembershipsDomId,
                """
                <div class="hard-box mt-5">
                    <strong class="p-3">Unknown channels</strong>
                    <pre>Subject Member's org does not have API Token.</pre>
                </div>
                """);
        }
        var response = await _slackApiClient.GetUsersConversationsAsync(
            apiToken,
            types: "public_channel,private_channel",
            user: SubjectMember.User.PlatformUserId);

        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("User.SlackUserConversations", "Viewed"),
                Actor = Viewer,
                Organization = Organization,
                Description = "Staff member viewed `users.conversations` Slack API response",
                StaffPerformed = true,
                StaffOnly = true,
            });

        var count = response.Ok ? $"{response.Body.Count}" : "unknown";

        var json = JsonConvert.SerializeObject(response, Formatting.Indented);
        return TurboUpdate(ChannelMembershipsDomId,
            $"""
            <div class="hard-box mt-5">
                <strong class="p-3">{count} channels</strong>
                <pre>{WebUtility.HtmlEncode(json)}</pre>
            </div>
            """);
    }

    public async Task<IActionResult> OnPostAssignAsync(string orgId, string id, string role, string reason)
    {
        if (reason is not { Length: > 0 })
        {
            StatusMessage = "You must provide a reason when unassigning/assigning roles.";
            return RedirectToPage();
        }

        await InitializeDataAsync(orgId);
        var member = Organization
            .Members
            .SingleOrDefault(m => string.Equals(m.User.PlatformUserId, id, StringComparison.OrdinalIgnoreCase));
        if (member is null)
        {
            StatusMessage = "Member not found";
            return RedirectToPage();
        }

        var memberRole = member.MemberRoles.SingleOrDefault(r => r.Role.Name == role);
        if (memberRole is not null)
        {
            StatusMessage = $"{member.DisplayName} is already assigned to that role";
            return RedirectToPage();
        }

        await _roleManager.AddUserToRoleAsync(member, role, Viewer, staffReason: reason);
        StatusMessage = $"Assigned {member.DisplayName} ({member.User.PlatformUserId}) to the '{role}' role.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnassignAsync(string orgId, string id, string role, string reason)
    {
        if (reason is not { Length: > 0 })
        {
            StatusMessage = "You must provide a reason when unassigning/assigning roles.";
            return RedirectToPage();
        }

        await InitializeDataAsync(orgId);
        var member = Organization.Members.SingleOrDefault(m =>
            string.Equals(m.User.PlatformUserId, id, StringComparison.OrdinalIgnoreCase));
        if (member is null)
        {
            StatusMessage = "Member not found";
            return RedirectToPage();
        }

        var memberRole = member.MemberRoles.SingleOrDefault(r => r.Role.Name == role);
        if (memberRole is null)
        {
            StatusMessage = $"{member.DisplayName} is not assigned to that role";
            return RedirectToPage();
        }

        await _roleManager.RemoveUserFromRoleAsync(member, role, Viewer, staffReason: reason);
        StatusMessage = $"Unassigned {member.DisplayName} ({member.User.PlatformUserId}) from the '{role}' role.";
        return RedirectToPage();
    }

    protected override Task InitializeDataAsync(Entities.Organization organization)
    {
        return Task.CompletedTask;
    }

    async Task<IActionResult?> LoadMemberAsync(string orgId, string id)
    {
        await InitializeDataAsync(orgId);
        var member = Organization
            .Members
            .SingleOrDefault(m => string.Equals(m.User.PlatformUserId, id, StringComparison.OrdinalIgnoreCase));

        if (member is null)
        {
            return NotFound("Member not found");
        }

        SubjectMember = member;

        await Db.Entry(member.User)
            .Collection(u => u.Members)
            .Query()
            .Include(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role)
            .Include(m => m.Organization)
            .LoadAsync();
        var memberships = member.User.Members;
        Memberships = new PaginatedList<Member>(memberships, memberships.Count, 1, memberships.Count);

        return null;
    }
}

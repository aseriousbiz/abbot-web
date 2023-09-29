using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Settings.Organization;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore.ModelBinding;
using Serious.Logging;

namespace Serious.Abbot.Pages.Settings.Rooms;

public class RoomPage : MetadataEditorPage<Room, string>, IResponseTimesSettingsContainer
{
    readonly ILogger<RoomPage> _log = ApplicationLoggerFactory.CreateLogger<RoomPage>();

    public DomId HubSpotLinkDomId { get; } = new("hubspot-linking");
    public DomId ZendeskLinkDomId { get; } = new("zendesk-linking");
    public DomId ConversationTrackingDomId { get; } = new("conversation-tracking-editor");
    public DomId ConversationSettingsDomId { get; } = new("conversation-settings-editor");
    public DomId ResponseTimesDomId { get; } = new("response-times-editor");
    public DomId FirstRespondersDomId { get; } = RespondersDomId(RoomRole.FirstResponder);
    public DomId EscalationRespondersDomId { get; } = RespondersDomId(RoomRole.EscalationResponder);

    static DomId RespondersDomId(RoomRole roomRole) => new($"{roomRole}s-editor");

    static string RespondersPartial(RoomRole roomRole) => $"_{roomRole}s";

    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly IClock _clock;
    readonly IIntegrationRepository _integrationRepository;
    readonly IHubSpotClientFactory _hubSpotClientFactory;
    readonly IZendeskClientFactory _zendeskClientFactory;
    readonly ISettingsManager _settingsManager;
    readonly IAuditLog _auditLog;

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Room",
            new { OrgId = Organization.PlatformId, Id = Room.PlatformRoomId });

    [BindProperty]
    public override List<EntityMetadataInput> EntityMetadataInputs { get; set; } = new();

    public RespondersContainer FirstResponders { get; private set; } = null!;

    public RespondersContainer EscalationResponders { get; private set; } = null!;

    public IReadOnlyList<Member> DefaultFirstResponders { get; private set; } = null!;

    public IReadOnlyList<Member> DefaultEscalationResponders { get; private set; } = null!;

    public Room Room { get; set; } = null!;

    public bool HasOrganizationDefaults => Organization.DefaultTimeToRespond.Deadline is not null
                                           || Organization.DefaultTimeToRespond.Warning is not null;

    public IReadOnlyList<WorkingHours> FirstResponderCoverage { get; set; } = Array.Empty<WorkingHours>();

    [BindProperty]
    public ResponseTimeSettings ResponseTimeSettings { get; set; } = null!;

    [BindProperty]
    public string? OrganizationId { get; set; }

    [BindProperty]
    public bool IsCommunitySupportRoom { get; set; }

    public IntegrationRoomLink? HubSpotRoomLink { get; private set; }

    public IntegrationRoomLink? ZendeskRoomLink { get; private set; }

    public bool ReadOnly => !Organization.HasPlanFeature(PlanFeature.ConversationTracking)
                            || !User.CanManageConversations();

    public RoomPage(
        IUserRepository userRepository,
        IRoomRepository roomRepository, IClock clock,
        IIntegrationRepository integrationRepository,
        IHubSpotClientFactory hubSpotClientFactory,
        IZendeskClientFactory zendeskClientFactory,
        IMetadataRepository metadataRepository,
        ISettingsManager settingsManager,
        IAuditLog auditLog) : base(metadataRepository)
    {
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _clock = clock;
        _integrationRepository = integrationRepository;
        _hubSpotClientFactory = hubSpotClientFactory;
        _zendeskClientFactory = zendeskClientFactory;
        _settingsManager = settingsManager;
        _auditLog = auditLog;
    }

    protected override async Task<Room?> InitializePageAsync(string entityId)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(entityId, Organization);
        if (room is null)
        {
            return null;
        }
        _log.BeginRoomScope(room);

        await InitializeMetadataAsync(room);

        IsCommunitySupportRoom = room.Settings?.IsCommunityRoom is true;

        DefaultFirstResponders = await _userRepository.GetDefaultFirstRespondersAsync(Organization);
        DefaultEscalationResponders = await _userRepository.GetDefaultEscalationRespondersAsync(Organization);

        FirstResponders = new RespondersContainer(
            room.GetFirstResponders().ToList(),
            Array.Empty<Member>(), // Not needed here.
            Viewer,
            Organization,
            RoomRole.FirstResponder,
            $"a first responder for #{room.Name}");

        EscalationResponders = new RespondersContainer(
            room.GetEscalationResponders().ToList(),
            Array.Empty<Member>(), // Not needed here.
            Viewer,
            Organization,
            RoomRole.EscalationResponder,
            $"an escalation responder for #{room.Name}");

        // TODO: Could enumerate enabled ticketing integrations
        HubSpotRoomLink = await GetIntegrationRoomLink<HubSpotSettings>(room);

        ZendeskRoomLink = await GetIntegrationRoomLink<ZendeskSettings>(room);

        // Read organization and room settings
        OrganizationEmojiSetting = await ReactionHandler.GetAllowTicketReactionSetting(_settingsManager, Organization);
        RoomEmojiSetting = await ReactionHandler.GetAllowTicketReactionSetting(_settingsManager, room);

        Room = room;

        bool useCustomResponseTimes = !Organization.HasDefaultResponseTimes()
                                      || room.TimeToRespond.Deadline is not null
                                      || room.TimeToRespond.Warning is not null;

        var readOnly = !Organization.HasPlanFeature(PlanFeature.ConversationTracking)
                       || !Viewer.CanManageConversations();

        ResponseTimeSettings = useCustomResponseTimes
            ? ResponseTimeSettings.FromTimeToRespond(
                room.TimeToRespond,
                readOnly,
                useCustomResponseTimes)
            : ResponseTimeSettings.FromTimeToRespond(
                room.Organization.DefaultTimeToRespond,
                readOnly,
                useCustomResponseTimes);

        // Fetch the working hours for all the FRs in the room, converted to the local time zone.
        if (Viewer.TimeZone is { } tz)
        {
            FirstResponderCoverage = room.Assignments
                .Where(a => a is { Role: RoomRole.FirstResponder, Member.TimeZone: not null })
                .Select(a => a.Member)
                .CalculateCoverage(tz, WorkingHours.Default, _clock.UtcNow)
                .ToList();
        }

        return room;
    }

    async Task<IntegrationRoomLink?> GetIntegrationRoomLink<TSettings>(Room room)
        where TSettings : class, IIntegrationSettings, ITicketingSettings
    {
        if (await _integrationRepository.GetIntegrationAsync<TSettings>(room.Organization) is not
            ({ Enabled: true } integration, { } settings))
        {
            return null;
        }

        // UI differentiates between Integration not found (null) vs RoomLink not found (empty)
        return settings.FindLink(room, integration) ?? new();
    }

    public bool? RoomEmojiSetting { get; set; }
    public bool OrganizationEmojiSetting { get; set; }

    public async Task<IActionResult> OnGetAsync(string roomId)
    {
        var room = await InitializePageAsync(roomId);
        if (room is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnGetAutocompleteZendeskOrganizations(string roomId, string? q)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return EmptyResult("Room not found.");
        }

        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(room.Organization);
        if (integration is not { Enabled: true })
        {
            return EmptyResult("Zendesk integration is not enabled.");
        }

        if (settings is not { HasApiCredentials: true })
        {
            return EmptyResult("Zendesk integration is not enabled.");
        }

        if (q is not { Length: > 0 })
        {
            return EmptyResult("You didn't specify a search query.");
        }

        var client = _zendeskClientFactory.CreateClient(settings);
        try
        {
            var organizations = await client.AutocompleteOrganizationsAsync(q);
            return Partial("_ZendeskOrganizationList",
                new ZendeskOrganizationListModel(
                    organizations.Body ?? Array.Empty<ZendeskOrganization>()));
        }
        catch (ApiException apiex)
        {
            _log.ErrorFetchingLinkCandidates(apiex);

            return EmptyResult("Unable to load organizations from Zendesk.");
        }

        IActionResult EmptyResult(string errorMessage) =>
            Partial("_ZendeskOrganizationList",
                new ZendeskOrganizationListModel(
                    Array.Empty<ZendeskOrganization>(),
                    errorMessage));
    }

    public async Task<IActionResult> OnGetAutocompleteHubSpotCompanies(string roomId, string? q)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return EmptyResult("Room not found.");
        }

        var (integration, settings) =
            await _integrationRepository.GetIntegrationAsync<HubSpotSettings>(room.Organization);
        if (integration is not { Enabled: true })
        {
            return EmptyResult("HubSpot integration is not enabled.");
        }

        if (integration.ExternalId is null
            || settings is not { HasApiCredentials: true })
        {
            return EmptyResult("HubSpot integration is not enabled.");
        }

        if (q is not { Length: > 0 })
        {
            return EmptyResult("You didn't specify a search query.");
        }

        var hubId = long.Parse(integration.ExternalId, CultureInfo.InvariantCulture);
        var client = await _hubSpotClientFactory.CreateClientAsync(integration, settings);
        try
        {
            var searchResult = await client.SearchAsync("companies",
                new("name", SearchOperator.ContainsToken, $"*{q}*"));
            return Partial("_HubSpotCompanyList",
                new HubSpotCompanyListModel(
                    searchResult.Results ?? Array.Empty<HubSpotSearchResult>(),
                    hubId));
        }
        catch (ApiException apiex)
        {
            _log.ErrorFetchingLinkCandidates(apiex);

            return EmptyResult("Unable to load companies from HubSpot.");
        }

        IActionResult EmptyResult(string errorMessage) =>
            Partial("_HubSpotCompanyList",
                new HubSpotCompanyListModel(
                    Array.Empty<HubSpotSearchResult>(),
                    ErrorMessage: errorMessage));
    }

    public async Task<IActionResult> OnPostAsync(string roomId)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        Room = room;

        if (!ResponseTimeSettings.UseCustomResponseTimes)
        {
            await _roomRepository.UpdateResponseTimesAsync(room, null, null, Viewer);
            return TurboStream(
                TurboFlash("Room updated to use organization defaults for response times."),
                TurboUpdate(
                    ResponseTimesDomId,
                    Partial("_ResponseTimesForm", this)));
        }

        static TimeSpan? GetTimeSpan(int? value, TimeUnits units) => value is not > 0
            ? null
            : units switch
            {
                TimeUnits.Days => TimeSpan.FromDays(value.Value),
                TimeUnits.Hours => TimeSpan.FromHours(value.Value),
                TimeUnits.Minutes => TimeSpan.FromMinutes(value.Value),
                _ => throw new InvalidOperationException($"Invalid time units: {units}")
            };

        var newTarget = GetTimeSpan(ResponseTimeSettings.TargetValue, ResponseTimeSettings.TargetUnits);
        var newDeadline = GetTimeSpan(ResponseTimeSettings.DeadlineValue, ResponseTimeSettings.DeadlineUnits);

        if (newDeadline <= newTarget)
        {
            ModelState.AddModelError($"{nameof(ResponseTimeSettings)}.{nameof(ResponseTimeSettings.DeadlineValue)}",
                "The deadline must be after the target.");
        }

        if (!ModelState.RemoveExcept(nameof(ResponseTimeSettings)).IsValid)
        {
            return TurboStream(
                TurboFlash("Error setting the response times", isError: true),
                TurboUpdate(
                    ResponseTimesDomId,
                    Partial("_ResponseTimesForm", this)));
        }

        await _roomRepository.UpdateResponseTimesAsync(room, newTarget, newDeadline, Viewer);
        return TurboStream(
            TurboFlash("Room settings updated!"),
            TurboUpdate(
                ResponseTimesDomId,
                Partial("_ResponseTimesForm", this)));
    }

    public async Task<IActionResult> OnPostConversationSettingsAsync(
        string roomId,
        bool useOrganizationDefault,
        bool allowTicketReactions)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        string auditMessage;
        string auditEvent;
        string statusMessage;
        if (useOrganizationDefault)
        {
            await _settingsManager.RemoveAsync(SettingsScope.Room(room),
                ReactionHandler.AllowTicketReactionSettingName,
                Viewer.User);
            statusMessage = "Reverted to organization defaults for 🎫 emoji reactions.";
            auditEvent = "RevertedToDefault";
            auditMessage = $"Reverted {room.Name} ({room.PlatformRoomId}) to organization defaults for 🎫 emoji reactions.";
        }
        else
        {
            await ReactionHandler.SetAllowTicketReactionSetting(
                _settingsManager,
                allowTicketReactions,
                Viewer.User,
                room);

            (statusMessage, auditEvent, auditMessage) = allowTicketReactions
                ? ("The 🎫 emoji reaction has been enabled for this room.",
                    "Enabled",
                    $"Enabled 🎫 emoji reaction in {room.Name} ({room.PlatformRoomId}).")
                : ("The 🎫 emoji reaction has been disabled for this room.",
                    "Disabled",
                    $"Disabled 🎫 emoji reaction in {room.Name} ({room.PlatformRoomId}).");
        }

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Room.Reactions.Ticket", auditEvent),
                Actor = Viewer,
                Organization = Organization,
                Description = auditMessage,
            });

        await InitializePageAsync(roomId);
        return TurboStream(
            TurboFlash(statusMessage),
            TurboUpdate(
                ConversationSettingsDomId,
                Partial("_ConversationSettings", this)));
    }

    public Task<IActionResult> OnPostUnlinkZendeskOrganizationAsync(string roomId) =>
        UnlinkAsync(roomId, RoomLinkType.ZendeskOrganization, ZendeskLinkDomId, "_ZendeskLinking");

    public Task<IActionResult> OnPostUnlinkHubSpotCompanyAsync(string roomId) =>
        UnlinkAsync(roomId, RoomLinkType.HubSpotCompany, HubSpotLinkDomId, "_HubSpotLinking");

    async Task<IActionResult> UnlinkAsync(string roomId, RoomLinkType roomLinkType, DomId target, string partialName)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return TurboFlash("Unable to find the requested room.");
        }

        if (!Viewer.CanManageConversations())
        {
            return TurboFlash("You do not have permission to manage room links.");
        }

        // Remove any existing organization links (should only be one, but we do a loop just in case).
        // We need to pull this into a separate list to prevent "Collection modified while enumerating" errors.
        var existingLinks = room.Links.Where(l => l.LinkType == roomLinkType).ToList();
        foreach (var link in existingLinks)
        {
            await _roomRepository.RemoveLinkAsync(link, Viewer);
        }

        await InitializePageAsync(roomId);

        return TurboStream(
            TurboFlash($"Removed {roomLinkType.Humanize()} link."),
            TurboUpdate(target, Partial(partialName, this)));
    }

    public Task<IActionResult> OnPostSetZendeskOrganizationAsync(string roomId,
        string organizationUrl, string organizationName) =>
        SetRoomLinkAsync(roomId, RoomLinkType.ZendeskOrganization, ZendeskLinkDomId, "_ZendeskLinking",
            organizationUrl, organizationName);

    public Task<IActionResult> OnPostSetHubSpotCompanyAsync(string roomId,
        long hubId, string companyId, string displayName) =>
        SetRoomLinkAsync(roomId, RoomLinkType.HubSpotCompany, HubSpotLinkDomId, "_HubSpotLinking",
            new HubSpotCompanyLink(hubId, companyId).ToString(), displayName);

    async Task<IActionResult> SetRoomLinkAsync(string roomId, RoomLinkType roomLinkType,
        DomId target, string partialName,
        string externalId, string displayName)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return TurboFlash("Unable to find the requested room.");
        }

        if (!Viewer.CanManageConversations())
        {
            return TurboFlash("You do not have permission to manage room links.");
        }

        // Remove any existing organization links (should only be one, but we do a loop just in case).
        // We need to pull this into a separate list to prevent "Collection modified while enumerating" errors.
        var existingLinks = room.Links.Where(l => l.LinkType == roomLinkType).ToList();
        foreach (var link in existingLinks)
        {
            await _roomRepository.RemoveLinkAsync(link, Viewer);
        }

        // Create the new link
        await _roomRepository.CreateLinkAsync(room,
            roomLinkType,
            externalId,
            displayName,
            Viewer,
            DateTime.UtcNow);

        await InitializePageAsync(roomId);

        return TurboStream(
            TurboFlash($"Room successfully linked to {roomLinkType.Humanize()} '{displayName}'"),
            TurboUpdate(target, Partial(partialName, this)));
    }

    public async Task<IActionResult> OnPostUnassignAsync(string roomId, Id<Member> memberId, RoomRole roomRole)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        Room = room;

        if (!room.ManagedConversationsEnabled)
        {
            return TurboFlash("Abbot is not tracking conversations in this room.");
        }

        var member = await _userRepository.GetMemberByIdAsync(memberId, Organization);
        if (member is null)
        {
            return TurboFlash("Member not found.");
        }

        var roleDescription = GetRoleMemberDescription(roomRole);
        if (!room.Assignments.Any(a => a.MemberId == memberId && a.Role == roomRole))
        {
            return TurboFlash($"{member.DisplayName} is not {roleDescription} in this room.");
        }

        await _roomRepository.UnassignMemberAsync(room, member, roomRole, Viewer);
        await InitializePageAsync(roomId);
        return TurboStream(
            TurboFlash($"{member.DisplayName} is no longer {roleDescription} for this room."),
            TurboUpdate(
                RespondersDomId(roomRole),
                Partial(RespondersPartial(roomRole), this)));
    }

    public async Task<IActionResult> OnPostAssignAsync(
        string roomId,
        Id<Member> memberId,
        RoomRole roomRole)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        Room = room;

        if (!room.ManagedConversationsEnabled)
        {
            return TurboFlash("Abbot is not tracking conversations in this room.");
        }

        var member = await _userRepository.GetMemberByIdAsync(memberId, Organization);
        if (member is null)
        {
            return TurboFlash("Member not found.");
        }

        var roleDescription = GetRoleMemberDescription(roomRole);

        if (room.Assignments.Any(a => a.MemberId == memberId && a.Role == roomRole))
        {
            return TurboFlash($"{member.DisplayName} is already {roleDescription} in this room.");
        }

        if (!member.IsAgent())
        {
            // Since we're filtering out non-members, this should never happen in practice, but could happen in
            // theory with a race condition, etc. That's why we handle it.
            return TurboFlash($"{WebConstants.ErrorStatusPrefix}{member.DisplayName} must log in to this site and be assigned the Agent role first, before being added as {roleDescription}.");
        }

        if (await _roomRepository.AssignMemberAsync(room, member, roomRole, Viewer))
        {
            await InitializePageAsync(roomId);

            return TurboStream(
                TurboFlash($"Assigned {member.DisplayName} as {roleDescription} for this room."),
                TurboUpdate(
                    RespondersDomId(roomRole),
                    Partial(RespondersPartial(roomRole), this)));
        }

        // This should never happen in practice, but good to have a condition for it anyways
        // since a weird race condition could cause it to happen.
        return TurboFlash($"Failed to assign {member.DisplayName} as {roleDescription} for this room.", isError: true);
    }

    public async Task<IActionResult> OnPostChangeRoomTypeAsync(string roomId)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        if (!room.ManagedConversationsEnabled)
        {
            return TurboFlash("Abbot is not tracking conversations in this room.");
        }

        room.Settings = (room.Settings ?? new RoomSettings()) with { IsCommunityRoom = IsCommunitySupportRoom };
        await _roomRepository.UpdateAsync(room);
        await InitializePageAsync(roomId);
        return TurboStream(
            TurboFlash("Room type updated."),
            TurboUpdate(
                ConversationTrackingDomId,
                Partial("_ConversationTrackingEditor", this)));
    }

    public async Task<IActionResult> OnPostUntrackAsync(string roomId)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        if (!room.ManagedConversationsEnabled)
        {
            return TurboFlash("Abbot is not tracking conversations in this room.");
        }

        await _roomRepository.SetConversationManagementEnabledAsync(room, enabled: false, Viewer);
        await InitializePageAsync(roomId);
        return TurboStream(
            TurboFlash("Abbot is no longer tracking conversations in this room."),
            TurboUpdate(
                ConversationTrackingDomId,
                Partial("_ConversationTrackingEditor", this)));
    }

    public async Task<IActionResult> OnPostTrackAsync(string roomId)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        if (room.ManagedConversationsEnabled)
        {
            return TurboFlash("Abbot is already tracking conversations in this room.");
        }

        await _roomRepository.SetConversationManagementEnabledAsync(room, enabled: true, Viewer);
        await InitializePageAsync(roomId);
        return TurboStream(
            TurboFlash("Abbot is now tracking conversations in this room."),
            TurboUpdate(
                ConversationTrackingDomId,
                Partial("_ConversationTrackingEditor", this)));
    }


    protected override async Task<Room?> GetEntityAsync(string entityId, Entities.Organization organization)
    {
        return await _roomRepository.GetRoomByPlatformRoomIdAsync(entityId, Organization);
    }

    protected override async Task UpdateEntityMetadataAsync(Room entity, Dictionary<string, string?> metadataUpdates, Member actor)
    {
        await MetadataRepository.UpdateRoomMetadataAsync(entity, metadataUpdates, Viewer);
    }

    public async Task<IActionResult> OnPostSaveMetadataAsync(string roomId)
    {
        return await HandlePostSaveMetadataAsync(roomId);
    }

    static string GetRoleMemberDescription(RoomRole roomRole)
    {
        return roomRole switch
        {
            RoomRole.EscalationResponder => "an escalation responder",
            RoomRole.FirstResponder => "a first responder",
            _ => throw new UnreachableException()
        };
    }
}

public record HubSpotCompanyListModel(
    IReadOnlyList<HubSpotSearchResult> Results,
    long HubId = 0,
    string? ErrorMessage = null);

public record ZendeskOrganizationListModel(
    IReadOnlyList<ZendeskOrganization> Organizations,
    string? ErrorMessage = null);

static partial class RoomPageLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Error fetching link candidates.")]
    public static partial void ErrorFetchingLinkCandidates(this ILogger<RoomPage> logger, Exception ex);
}

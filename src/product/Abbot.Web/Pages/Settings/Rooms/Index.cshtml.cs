using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Serious.Abbot.Entities;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Extensions;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Settings.Organization;
using Serious.Abbot.Pages.Shared.Filters;
using Serious.Abbot.Repositories;
using Serious.Abbot.Rooms;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.AspNetCore.ModelBinding;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Pages.Settings.Rooms;

public class RoomsIndexPage : MetadataManagerPage, IResponseTimesSettingsContainer
{
    public DomId DefaultFirstRespondersDomId { get; } = DefaultRespondersDomId(Scripting.RoomRole.FirstResponder);
    public DomId DefaultEscalationRespondersDomId { get; } = DefaultRespondersDomId(Scripting.RoomRole.EscalationResponder);

    static DomId DefaultRespondersDomId(RoomRole roomRole) => new($"default-{roomRole}s-editor");

    readonly IRoomRepository _roomRepository;
    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly CustomerRepository _customerRepository;
    readonly IRoleManager _roleManager;
    readonly IRoomJoiner _roomJoiner;
    readonly IOrganizationApiSyncer _organizationApiSyncer;
    readonly ISettingsManager _settingsManager;
    readonly FeatureService _featureService;

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Rooms", new { Id = Organization.PlatformId });

    [BindProperty]
    public int PageSize { get; set; } = WebConstants.LongPageSize;

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; } = new();

    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty]
    public Id<Customer>? CustomerId { get; set; }

    [BindProperty]
    public string? PlatformRoomId { get; set; }

    /// <summary>
    /// Whether we're showing tracked rooms or not.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public TrackStateFilter Tab { get; set; } = TrackStateFilter.Tracked;

    public CompositeFilterModel CustomerFilterModel { get; private set; } = null!;

    public FilterModel FirstResponderFilterModel { get; private set; } = null!;

    public FilterModel EscalationResponderFilterModel { get; private set; } = null!;

    public IReadOnlyList<SelectListItem> PageSizes { get; set; } = Array.Empty<SelectListItem>();

    public RoomCountsResult RoomCounts { get; private set; } = null!;

    public IReadOnlyList<Customer> AllCustomers { get; private set; } = Array.Empty<Customer>();

    [BindProperty]
    public ResponseTimeSettings ResponseTimeSettings { get; set; } = null!;

    public RespondersContainer DefaultFirstResponders { get; private set; } = null!;

    public RespondersContainer DefaultEscalationResponders { get; private set; } = null!;

    public IPaginatedList<Room> Rooms { get; set; } = null!;

    public UpdateMessagesModel UpdateMessagesInput { get; set; } = new(false, null, false, null);

    [BindProperty]
    public IList<string> RoomIds { get; set; } = Array.Empty<string>();

    [BindProperty]
    public IList<string> ResponderIds { get; set; } = Array.Empty<string>();

    [BindProperty]
    public string? DisableRoomPlatformId { get; set; }

    [BindProperty]
    public override MetadataFieldInput MetadataInput { get; set; } = new();

    [BindProperty]
    public override string? MetadataFieldToDelete { get; set; }

    public bool HasResponseTimesModelError => ModelState[$"{nameof(ResponseTimeSettings)}.{nameof(ResponseTimeSettings.TargetValue)}"]?.Errors.Any() is true
        || ModelState[$"{nameof(ResponseTimeSettings)}.{nameof(ResponseTimeSettings.DeadlineValue)}"]?.Errors.Any() is true;

    [BindProperty]
    public RoomRole? RoomRole { get; set; }

    public RoomsIndexPage(
        IRoomRepository roomRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        CustomerRepository customerRepository,
        IRoleManager roleManager,
        IRoomJoiner roomJoiner,
        IMetadataRepository metadataRepository,
        IOrganizationApiSyncer organizationApiSyncer,
        ISettingsManager settingsManager,
        FeatureService featureService) : base(metadataRepository, MetadataFieldType.Room)
    {
        _roomRepository = roomRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _roleManager = roleManager;
        _roomJoiner = roomJoiner;
        _organizationApiSyncer = organizationApiSyncer;
        _settingsManager = settingsManager;
        _featureService = featureService;
    }

    // I really like clean URLs and we don't need query string parameters for the default values.
    RedirectToPageResult RedirectToCurrentPage()
    {
        var routeValues = new RouteValueDictionary();
        if (PageNumber > 1)
        {
            routeValues.Add("p", PageNumber);
        }

        if (Tab != TrackStateFilter.Tracked)
        {
            routeValues.Add("tab", Tab);
        }

        if (Filter.Any())
        {
            routeValues.Add("q", Filter.ToString());
        }

        return RedirectToPage(routeValues);
    }

    async Task InitializeAsync()
    {
        AllCustomers = await _customerRepository.GetAllAsync(Organization);
        var segments = await _customerRepository.GetAllCustomerSegmentsAsync(Organization);
        CustomerFilterModel = FilterModel.CreateCompositeFilterModel(
            FilterModel.Create(AllCustomers, Filter, field: "customer", label: "Customers", c => c.Name, "segment"),
            FilterModel.Create(segments, Filter, field: "segment", label: "Segments", c => c.Name, "customer"),
            "Customers");

        CustomerFilterModel = AbbotFilterModelHelpers.CreateCustomerFilterModel(
            AllCustomers,
            segments,
            Filter);

        PageSize = await _settingsManager.GetIntegerValueAsync(
            SettingsScope.Member(Viewer),
            name: "Rooms:PageSize",
            defaultIfNull: WebConstants.ShortPageSize);

        var pageSizes = new[] { 10, 20, 100, int.MaxValue, };
        PageSizes = pageSizes
            .Select(x => new SelectListItem(
                x is int.MaxValue ? "All" : $"{x}",
                $"{x}",
                x == PageSize))
            .ToList();

        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        FirstResponderFilterModel = AbbotFilterModelHelpers.CreateResponderModel(
            agents,
            Filter,
            field: "fr",
            label: "First responders");
        EscalationResponderFilterModel = AbbotFilterModelHelpers.CreateResponderModel(
            agents,
            Filter,
            field: "er",
            label: "Escalation responders");

        DefaultFirstResponders = await GetDefaultResponders(Scripting.RoomRole.FirstResponder, agents);

        DefaultEscalationResponders = await GetDefaultResponders(Scripting.RoomRole.EscalationResponder, agents);

        // Fetch rooms
        Rooms = await _roomRepository.GetPersistentRoomsAsync(
            Organization,
            Filter,
            Tab,
            PageNumber,
            PageSize);

        RoomCounts = await _roomRepository.GetPersistentRoomCountsAsync(Organization, Filter);

        ResponseTimeSettings = ResponseTimeSettings.FromTimeToRespond(
            Organization.DefaultTimeToRespond,
            !Organization.HasPlanFeature(PlanFeature.ConversationTracking),
            false);

        await InitializeMetadataAsync();
    }

    async Task<RespondersContainer> GetDefaultResponders(RoomRole roomRole, IReadOnlyList<Member> agents)
    {
        var (members, description) = roomRole switch
        {
            Scripting.RoomRole.FirstResponder => (await _userRepository.GetDefaultFirstRespondersAsync(Organization), "a first responder for this organization"),
            Scripting.RoomRole.EscalationResponder => (await _userRepository.GetDefaultEscalationRespondersAsync(Organization), "an escalation responder for this organization"),
            _ => throw new UnreachableException($"{roomRole}"),
        };

        return new RespondersContainer(
            members,
            agents,
            Viewer,
            Organization,
            roomRole,
            description);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Enum.IsDefined(Tab))
        {
            return NotFound();
        }
        await InitializeAsync();

        if (PageNumber > 1 && Rooms.TotalPages < PageNumber || PageNumber < 1)
        {
            PageNumber = Math.Max(Rooms.TotalPages, 1);
            // Redirect to last page if the current page is out of range. Never try to redirect if we're already
            // on page 1 as that just means there are no records.
            return RedirectToCurrentPage();
        }

        var orgSettings = RoomSettings.Merge(RoomSettings.Default, Organization.DefaultRoomSettings);
        UpdateMessagesInput = new(
            orgSettings.WelcomeNewUsers ?? false,
            orgSettings.UserWelcomeMessage ?? string.Empty,
            orgSettings.WelcomeNewConversations ?? false,
            orgSettings.ConversationWelcomeMessage ?? string.Empty);

        return Page();
    }

    public async Task<IActionResult> OnPostSetPageSizeAsync()
    {
        await _settingsManager.SetIntegerValueAsync(
            SettingsScope.Member(Viewer),
            name: "Rooms:PageSize",
            PageSize,
            Viewer.User);

        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostRefreshRoomsAsync()
    {
        await _organizationApiSyncer.UpdateRoomsFromApiAsync(Organization);
        StatusMessage = "Rooms updated from Slack.";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostUpdateDefaultResponseTimesAsync()
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        ModelState.RemoveExcept(nameof(ResponseTimeSettings));

        if (!ModelState.IsValid)
        {
            await InitializeAsync();

            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Could not save response time settings.";
            return Page();
        }

        var newTarget = GetTimeSpan(ResponseTimeSettings.TargetValue, ResponseTimeSettings.TargetUnits);
        var newDeadline = GetTimeSpan(ResponseTimeSettings.DeadlineValue, ResponseTimeSettings.DeadlineUnits);

        if (newDeadline <= newTarget)
        {
            await InitializeAsync();

            ModelState.AddModelError($"{nameof(ResponseTimeSettings)}.{nameof(ResponseTimeSettings.DeadlineValue)}", "The deadline must be after the target.");
            return Page();
        }

        Organization.DefaultTimeToRespond = new Threshold<TimeSpan>(newTarget, newDeadline);

        await _organizationRepository.SaveChangesAsync();
        StatusMessage = newTarget is not null && newDeadline is not null
            ? "Default Response Time settings updated!"
            : "Default Response Time settings cleared!";

        return RedirectToCurrentPage();
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

    public async Task<IActionResult> OnPostSaveRoomsResponseTimesAsync()
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        var newTarget = ResponseTimeSettings.UseCustomResponseTimes
            ? GetTimeSpan(ResponseTimeSettings.TargetValue, ResponseTimeSettings.TargetUnits)
            : null;
        var newDeadline = ResponseTimeSettings.UseCustomResponseTimes
            ? GetTimeSpan(ResponseTimeSettings.DeadlineValue, ResponseTimeSettings.DeadlineUnits)
            : null;

        if (newDeadline <= newTarget)
        {
            await InitializeAsync();

            ModelState.AddModelError($"{nameof(ResponseTimeSettings)}.{nameof(ResponseTimeSettings.DeadlineValue)}", "The deadline must be after the target.");
            return Page();
        }

        var results = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(
            RoomIds,
            Organization);

        foreach (var result in results)
        {
            if (result.Exists)
            {
                await _roomRepository.UpdateResponseTimesAsync(result.Room, newTarget, newDeadline, Viewer);
            }
        }

        StatusMessage = "Response Time settings updated!";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostSaveRespondersAsync()
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        RoomRole.Require();

        var results = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(
            RoomIds,
            Organization);
        foreach (var result in results)
        {
            if (result.Exists)
            {
                await _roomRepository.SetRoomAssignmentsAsync(result.Room, ResponderIds, RoomRole.Value, Viewer);
            }
        }

        StatusMessage = "Room assignments updated!";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostDisableTrackingAsync()
    {
        DisableRoomPlatformId.Require();

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(DisableRoomPlatformId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        await _roomRepository.SetConversationManagementEnabledAsync(room, enabled: false, Viewer);

        StatusMessage = $"Disabled conversation management for #{room.Name}";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostTrackConversationsAsync()
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToCurrentPage();
        }

        var results = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(RoomIds, Organization);
        foreach (var result in results)
        {
            if (result.Exists)
            {
                await _roomRepository.SetConversationManagementEnabledAsync(result.Room, enabled: true, Viewer);
            }
        }

        StatusMessage = "Enabled conversation management!";

        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostUpdateMessagesAsync(UpdateMessagesModel updateMessagesInput)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        var orgSettings = RoomSettings.Merge(RoomSettings.Default, Organization.DefaultRoomSettings);
        Organization.DefaultRoomSettings = orgSettings with
        {
            WelcomeNewUsers = updateMessagesInput.WelcomeNewUsers,
            UserWelcomeMessage = updateMessagesInput.UserWelcomeMessage,
            WelcomeNewConversations = updateMessagesInput.WelcomeNewConversations,
            ConversationWelcomeMessage = updateMessagesInput.ConversationWelcomeMessage
        };
        await _organizationRepository.SaveChangesAsync();
        StatusMessage = "Updated auto-responder settings.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddAbbotAsync()
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToCurrentPage();
        }

        var results = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(RoomIds, Organization);
        List<string> errors = new List<string>();
        foreach (var lookupResult in results)
        {
            if (lookupResult.Room is { } room
                && await _roomJoiner.JoinAsync(room, Viewer) is { Ok: false } result)
            {
                errors.Add(result.Error switch
                {
                    "channel_not_found" =>
                        $"<code>{room.Name}</code> was deleted.",
                    "is_archived" =>
                        $"<code>{room.Name}</code> was archived.",
                    "method_not_supported_for_channel_type" =>
                        $"<code>{room.Name}</code> is a channel type that is not supported.",
                    _ =>
                        $"<code>{room.Name}</code> had an error: {result.Error}.",
                });
            }
        }

        StatusMessage = !errors.Any()
            ? $"Added {Organization.BotName ?? "Abbot"} to the selected rooms!"
            : $"{WebConstants.ErrorStatusPrefix}There were some problems adding {Organization.BotName ?? "Abbot"} to the selected rooms: {string.Join("\n", errors)}.";

        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostAssignCustomerAsync()
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToCurrentPage();
        }

        if (PlatformRoomId is null)
        {
            StatusMessage = "You must select a room.";
            return RedirectToCurrentPage();
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(PlatformRoomId, Organization).Require();
        if (CustomerId is null)
        {
            StatusMessage = $"Customer unassigned from room {room.Name}";
            room.Customer = null;
        }
        else
        {
            var customer = await _customerRepository.GetByIdAsync(CustomerId.GetValueOrDefault(), Organization).Require();
            StatusMessage = $"Customer {customer.Name} assigned to room {room.Name}";
            room.Customer = customer;
        }
        await _customerRepository.AssignRoomAsync(room, CustomerId, Viewer);

        return RedirectToCurrentPage();
    }

    /// <summary>
    /// Assigns an Agent to be a default first responder
    /// </summary>
    /// <param name="memberId">The database ;`Id for the member.</param>
    /// <param name="roomRole">The room role to set for the member.</param>
    public async Task<IActionResult> OnPostAssignAsync(Id<Member> memberId, RoomRole roomRole)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var subject = await _userRepository.GetMemberByIdAsync(memberId, Organization);
        if (subject is null)
        {
            return TurboFlash("Member not found.");
        }

        var roleMemberDescription = GetRoleMemberDescription(roomRole);
        var responders = await GetRespondersForRoleAsync(roomRole);

        if (responders.Any(fr => fr.Id == memberId))
        {
            return TurboFlash($"{subject.DisplayName} is already {roleMemberDescription} for this organization.");
        }

        if (!subject.IsAgent())
        {
            // Since we're filtering out non-members, this should never happen in practice, but could happen in
            // theory with a race condition, etc. That's why we handle it.
            return TurboFlash($"{WebConstants.ErrorStatusPrefix}{subject.DisplayName} must log in to this site and be assigned the Agent role first, before being added as {roleMemberDescription}.");
        }

        if (await AssignResponderRoleAsync(subject, roomRole))
        {
            var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
            var updatedResponders = await GetDefaultResponders(roomRole, agents);
            return TurboStream(
                TurboFlash($"Assigned {subject.DisplayName} as  {roleMemberDescription} for this organization."),
                TurboUpdate(DefaultRespondersDomId(roomRole), Partial("_RespondersList", updatedResponders)));
        }

        // This should never happen in practice, but good to have a condition for it anyways
        // since a weird race condition could cause it to happen.
        return TurboFlash($"Failed to assign {subject.DisplayName} as {roleMemberDescription} for this organization.", isError: true);
    }

    public async Task<IActionResult> OnPostUnassignAsync(Id<Member> memberId, RoomRole roomRole)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        var subject = await _userRepository.GetMemberByIdAsync(memberId, Organization);
        if (subject is null)
        {
            return TurboFlash("Member not found.");
        }

        var roleMemberDescription = GetRoleMemberDescription(roomRole);
        var responders = await GetRespondersForRoleAsync(roomRole);

        if (responders.Any(fr => fr.Id == memberId) is false)
        {
            return TurboFlash($"{subject.DisplayName} is not {roleMemberDescription} for this organization.");
        }

        await UnassignResponderRoleAsync(subject, roomRole);

        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        var updatedResponders = await GetDefaultResponders(roomRole, agents);

        return TurboStream(
            TurboFlash($"{subject.DisplayName} is no longer {roleMemberDescription} for this organization."),
            TurboUpdate(DefaultRespondersDomId(roomRole), Partial("_RespondersList", updatedResponders)));
    }

    async Task<bool> AssignResponderRoleAsync(Member subject, RoomRole roomRole)
    {
        return roomRole switch
        {
            Scripting.RoomRole.FirstResponder => await _organizationRepository.AssignDefaultFirstResponderAsync(Organization,
                subject,
                Viewer),
            Scripting.RoomRole.EscalationResponder => await _organizationRepository.AssignDefaultEscalationResponderAsync(
                Organization,
                subject,
                Viewer),
            _ => throw new UnreachableException()
        };
    }

    async Task<bool> UnassignResponderRoleAsync(Member subject, RoomRole roomRole)
    {
        return roomRole switch
        {
            Scripting.RoomRole.FirstResponder => await _organizationRepository.UnassignDefaultFirstResponderAsync(Organization,
                subject,
                Viewer),
            Scripting.RoomRole.EscalationResponder => await _organizationRepository.UnassignDefaultEscalationResponderAsync(
                Organization,
                subject,
                Viewer),
            _ => throw new UnreachableException()
        };
    }

    static string GetRoleMemberDescription(RoomRole roomRole)
    {
        return roomRole switch
        {
            Scripting.RoomRole.EscalationResponder => "a default escalation responder",
            Scripting.RoomRole.FirstResponder => "a default first responder",
            _ => throw new UnreachableException()
        };
    }

    async Task<IReadOnlyList<Member>> GetRespondersForRoleAsync(RoomRole roomRole)
    {
        return roomRole switch
        {
            Scripting.RoomRole.FirstResponder => await _userRepository.GetDefaultFirstRespondersAsync(Organization),
            Scripting.RoomRole.EscalationResponder => await _userRepository.GetDefaultEscalationRespondersAsync(Organization),
            _ => throw new UnreachableException()
        };
    }

    public record UpdateMessagesModel(
        bool WelcomeNewUsers,
        string? UserWelcomeMessage,
        bool WelcomeNewConversations,
        string? ConversationWelcomeMessage);
}

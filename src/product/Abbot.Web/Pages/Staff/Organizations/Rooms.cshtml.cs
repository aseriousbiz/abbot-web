using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Slack;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class RoomsPage : OrganizationDetailPage
{
    readonly ISlackResolver _slackResolver;
    readonly ISlackApiClient _slackApiClient;
    readonly IIntegrationRepository _integrationRepository;
    readonly IRoomRepository _roomRepository;

    public static readonly DomId SlackApiResults = new("slack-api-results");
    public static readonly DomId ResolveButton = new("slack-resolve-button");

    public RoomsPage(
        AbbotContext db,
        ISlackResolver slackResolver,
        ISlackApiClient slackApiClient,
        IIntegrationRepository integrationRepository,
        IRoomRepository roomRepository,
        IAuditLog auditLog)
        : base(db, auditLog)
    {
        _slackResolver = slackResolver;
        _slackApiClient = slackApiClient;
        _integrationRepository = integrationRepository;
        _roomRepository = roomRepository;
    }

    public IList<Room> Rooms { get; set; } = null!;

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    [BindProperty(SupportsGet = true)]
    public RoomTypeFilter Type { get; set; } = RoomTypeFilter.Persistent;

    [BindProperty(SupportsGet = true)]
    public bool IncludeDeleted { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool IncludeArchived { get; set; }

    [BindProperty]
    public string? Channel { get; set; }

    [BindProperty]
    [Required]
    public string? Reason { get; set; }

    [BindProperty]
    public bool IncludeRecentMessages { get; set; }

    public int ConversationRoomCount { get; set; }

    public int AbbotMemberCount { get; set; }

    public int RoomsWithResponseTimesSet { get; private set; }
    public int RoomsWithFirstResponders { get; private set; }
    public int RoomsWithEscalationResponders { get; private set; }

    public bool HasDefaultResponseTimesSet { get; private set; }
    public bool HasDefaultFirstResponders { get; private set; }
    public bool HasDefaultEscalationResponders { get; private set; }

    public string? ResponseJson { get; private set; }

    public bool AllowResolvingRoom { get; private set; }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        var query = Db.Rooms.Where(r => r.OrganizationId == organization.Id);

        query = Type switch
        {
            RoomTypeFilter.All => query,
            RoomTypeFilter.Persistent => query.Where(r => r.Persistent),
            RoomTypeFilter.NonPersistent => query.Where(r => !r.Persistent),
            RoomTypeFilter.DirectMessage => query.Where(r => r.RoomType == RoomType.DirectMessage),
            RoomTypeFilter.MultiPartyDirectMessage => query.Where(r => r.RoomType == RoomType.MultiPartyDirectMessage),
            RoomTypeFilter.PrivateChannel => query.Where(r => r.RoomType == RoomType.PrivateChannel),
            RoomTypeFilter.PublicChannel => query.Where(r => r.RoomType == RoomType.PublicChannel),
            RoomTypeFilter.TrackingEnabled => query.Where(r => r.ManagedConversationsEnabled),
            RoomTypeFilter.Unknown => query.Where(r => r.RoomType == RoomType.Unknown),
            var f => throw new UnreachableException($"Unknown room type filter: {f}")
        };

        if (Filter is { Length: > 0 })
        {
            // ToLower is what Postgres uses for case-insensitive matching, so we should too.
            // https://www.postgresql.org/docs/current/citext.html
            var filter = Filter.ToLowerInvariant();

            // ToLowerInvariant isn't mapped by Npgsql.
#pragma warning disable CA1304
            query = query.Where(r => r.Name!.ToLower().Contains(filter) || r.PlatformRoomId.ToLower() == filter);
#pragma warning restore CA1304
        }

        query = IncludeDeleted
            ? query
            : query.Where(r => r.Deleted != true);
        query = IncludeArchived
            ? query
            : query.Where(r => r.Archived != true);

        query = query
            .OrderBy(r => r.Deleted == true
                ? 2
                : (r.Archived == true
                    ? 1
                    : 0))
            .ThenBy(r => r.Name);

        Rooms = await query.ToListAsync();

        // Get some global counts
        AbbotMemberCount = Rooms.Count(r => r.OrganizationId == organization.Id && r.BotIsMember == true);
        ConversationRoomCount = Rooms.Count(r => r.OrganizationId == organization.Id && r.ManagedConversationsEnabled);

        HasDefaultResponseTimesSet = organization.HasDefaultResponseTimes();
        RoomsWithResponseTimesSet = Rooms.Count(r => r.HasCustomResponseTimes());

        RoomsWithFirstResponders = await Db.RoomAssignments
            .Where(a => a.Role == RoomRole.FirstResponder)
            .Where(a => a.Member.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .Where(a => a.Room.OrganizationId == organization.Id)
            .GroupBy(a => a.RoomId)
            .CountAsync();

        RoomsWithEscalationResponders = await Db.RoomAssignments
            .Where(a => a.Role == RoomRole.EscalationResponder)
            .Where(a => a.Member.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .Where(a => a.Room.OrganizationId == organization.Id)
            .GroupBy(a => a.RoomId)
            .CountAsync();

        HasDefaultFirstResponders = await Db.Members
            .Where(m => m.IsDefaultFirstResponder)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .Where(m => m.OrganizationId == organization.Id)
            .AnyAsync();

        HasDefaultEscalationResponders = await Db.Members
            .Where(m => m.IsDefaultEscalationResponder)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .Where(m => m.OrganizationId == organization.Id)
            .AnyAsync();
    }

    public async Task OnGetAsync(string id, string? channel = null)
    {
        Channel = channel;
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostRefreshMetadataAsync(string id, int? roomId)
    {
        if (roomId is null)
        {
            StatusMessage = "Room not found";
            return RedirectToPage();
        }

        await InitializeDataAsync(id);

        if (Organization.PlatformType != PlatformType.Slack)
        {
            StatusMessage = "Can only refresh rooms in Slack organizations";
            return RedirectToPage();
        }

        var room = await Db.Rooms.SingleOrDefaultAsync(r => r.OrganizationId == Organization.Id && r.Id == roomId);
        if (room is null)
        {
            StatusMessage = "Room not found";
            return RedirectToPage();
        }

        await _slackResolver.ResolveRoomAsync(room.PlatformRoomId, Organization, forceRefresh: true);
        StatusMessage = "Metadata refreshed";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResolveRoomAsync(string id)
    {
        if (Channel is not { Length: > 0 })
        {
            StatusMessage = "No channel specified";
            return RedirectToPage();
        }

        await InitializeDataAsync(id);

        var resolved = await _slackResolver.ResolveRoomAsync(Channel, Organization, forceRefresh: true);
        return resolved is not null
            ? TurboFlash("Room resolved")
            : TurboFlash("Something went wrong", isError: true);
    }

    public async Task<IActionResult> OnPostCallSlackApiAsync(string id)
    {
        Channel = Channel?.Trim();

        if (Channel is not { Length: > 0 })
        {
            StatusMessage = "No channel specified";
            return RedirectToPage();
        }

        if (Reason is not { Length: > 0 })
        {
            StatusMessage = "No reason specified";
            return RedirectToPage(new {
                channel = Channel
            });
        }

        await InitializeDataAsync(id);

        if (!Organization.Integrations.IsLoaded)
        {
            await Db.Entry(Organization).Collection(o => o.Integrations).LoadAsync();
        }

        var (slackIntegration, slackAppSettings) = await _integrationRepository
            .GetIntegrationAsync<SlackAppSettings>(Organization);

        var (defaultAuth, customAuth) = slackAppSettings?.Authorization is null
            ? (new SlackAuthorization(Organization), null)
            : (slackAppSettings.DefaultAuthorization.Require(), slackAppSettings.Authorization);

        var authInEffect = (customAuth ?? defaultAuth).Require();
        var apiToken = authInEffect.RequireAndRevealApiToken();

        var defaultResponse = await _slackApiClient.Conversations.GetConversationInfoAsync(
            apiToken,
            Channel,
            includeLocale: true,
            includeMemberCount: true);

        string FormatAuthorization(string prefix, SlackAuthorization auth, bool enabled, ConversationInfoResponse resp)
        {
            var enabledMessage = enabled ? "<strong>is enabled</strong>" : "is not enabled";
            var membershipStatus = resp switch
            {
                { Ok: false } => $"membership check failed: {resp}",
                { Ok: true, Body.IsMember: true } => "<strong>is a member</strong> of this channel",
                { Ok: true, Body.IsMember: false } => "is not a member of this channel",
            };
            return $"{prefix}: {auth.AppName} ({auth.BotUserId}) {enabledMessage} and {membershipStatus}";
        }

        var defaultBotEnabled = slackIntegration is null || !slackIntegration.Enabled;
        var defaultBotInfo = FormatAuthorization("Default", defaultAuth, defaultBotEnabled, defaultResponse);

        string? customBotInfo;
        if (customAuth is not null && slackIntegration is not null)
        {
            var customAuthApiToken = customAuth.RequireAndRevealApiToken();

            var customResponse = await _slackApiClient.Conversations.GetConversationInfoAsync(
                customAuthApiToken,
                Channel,
                includeLocale: true,
                includeMemberCount: true);
            customBotInfo = FormatAuthorization("Custom", customAuth, slackIntegration.Enabled, customResponse);
        }
        else
        {
            customBotInfo = "No custom bot configured.";
        }

        var responseJson = JsonConvert.SerializeObject(defaultResponse, Formatting.Indented);
        string? recentMessagesJson = null;
        if (IncludeRecentMessages)
        {
            var recentMessagesResponse =
                await _slackApiClient.Conversations.GetConversationHistoryAsync(apiToken, Channel, limit: 10, includeAllMetadata: true);
            recentMessagesJson = JsonConvert.SerializeObject(recentMessagesResponse, Formatting.Indented);
        }

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(Channel, Organization);
        var roomInfo = room is null ? "Room does not exist in our db" : $"Room exists in our db with id {room.Id}";
        AllowResolvingRoom = room is null;

        var responseText = $"""

{defaultBotInfo}
{customBotInfo}

{roomInfo}

{responseJson}

{recentMessagesJson}
""";

        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Room.SlackChannel.ApiInfo", "Viewed"),
                Actor = Viewer,
                Organization = Organization,
                Description = $"Viewed Slack API info for channel: {Channel}.",
                StaffOnly = true,
                StaffPerformed = true,
                StaffReason = Reason,
            });

        return TurboStream(
            TurboUpdate(SlackApiResults, responseText),
            TurboUpdate(ResolveButton, Partial("_ResolveButton", this)));
    }

    public enum RoomTypeFilter
    {
        Persistent,
        [Display(Name = "Non-Persistent")]
        NonPersistent,
        [Display(Name = "All Types")]
        All,
        [Display(Name = "Public Channels")]
        PublicChannel,
        [Display(Name = "Private Channels")]
        PrivateChannel,
        [Display(Name = "Direct Messages")]
        DirectMessage,
        [Display(Name = "Direct Message Groups")]
        MultiPartyDirectMessage,
        [Display(Name = "Tracking Enabled")]
        TrackingEnabled,
        [Display(Name = "Unknown")]
        Unknown,
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class RoomPage : OrganizationDetailPage
{
    readonly ISettingsManager _settingsManager;
    readonly IRoomRepository _roomRepository;
    readonly IHubRepository _hubRepository;

    public DomId SettingsListDomId => new("settings-list");

    public Room SubjectRoom { get; set; } = null!;
    public Hub? Hub { get; set; }

    [BindProperty]
    public string? LastVerifiedMessageId { get; set; }

    public bool OrganizationAllowTicketReactionSetting { get; private set; }

    public bool? RoomEmojiSetting { get; private set; }

    public RoomPage(
        AbbotContext db,
        IRoomRepository roomRepository,
        ISettingsManager settingsManager,
        IHubRepository hubRepository,
        IAuditLog auditLog) : base(db, auditLog)
    {
        _settingsManager = settingsManager;
        _roomRepository = roomRepository;
        _hubRepository = hubRepository;
    }

    public async Task<IActionResult> OnGetAsync(string orgId, string id)
    {
        await InitializeDataAsync(orgId);

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(id, Organization);
        if (room is null)
        {
            return NotFound("Room not found");
        }

        // Read organization and room settings
        OrganizationAllowTicketReactionSetting = await ReactionHandler.GetAllowTicketReactionSetting(_settingsManager, Organization);
        RoomEmojiSetting = await ReactionHandler.GetAllowTicketReactionSetting(_settingsManager, room);

        LastVerifiedMessageId = await _settingsManager.GetLastVerifiedMessageIdAsync(room);

        Hub = await _hubRepository.GetHubAsync(room);
        SubjectRoom = room;

        return Page();
    }

    public SettingsScope Scope() => SettingsScope.Room(SubjectRoom);

    public async Task<IActionResult> OnPostSettingsAsync(string orgId, string id)
    {
        await InitializeDataAsync(orgId);

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(id, Organization);
        if (room is null)
        {
            return NotFound("Room not found");
        }

        SubjectRoom = room;

        var settings = await _settingsManager.GetAllAsync(Scope());
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    public async Task<IActionResult> OnPostSettingDeleteAsync(string orgId, string id, string name)
    {
        await InitializeDataAsync(orgId);

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(id, Organization);
        if (room is null)
        {
            return NotFound("Room not found");
        }

        SubjectRoom = room;

        var scope = Scope();
        if (await _settingsManager.GetAsync(scope, name) is not { } setting)
        {
            return TurboFlash($"Setting '{name}' not found.");
        }

        await _settingsManager.RemoveWithAuditingAsync(scope, name, Viewer.User, Organization);

        var settings = await _settingsManager.GetAllAsync(scope);
        return TurboUpdate(SettingsListDomId, Partial("_SettingsList", settings));
    }

    public async Task<IActionResult> OnPostUpdateLastMessageIdAsync(string orgId, string id)
    {
        await InitializeDataAsync(orgId);

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(id, Organization);
        if (room is null)
        {
            return NotFound("Room not found");
        }

        SubjectRoom = room;

        if (LastVerifiedMessageId is { Length: > 0 })
        {
            await _settingsManager.SetLastVerifiedMessageIdAsync(room, LastVerifiedMessageId, Viewer.User);
        }

        return TurboFlash("Last Verified Message Id Updated");
    }

    protected override Task InitializeDataAsync(Organization organization)
    {
        return Task.CompletedTask;
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Azure;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Slack;

namespace Serious.Abbot.Pages.Staff.Organizations.Hubs;

public class ViewModel : OrganizationDetailPage
{
    readonly IHubRepository _hubRepository;
    readonly IRoomRepository _roomRepository;
    readonly IEmojiLookup _emojiLookup;

    public required Hub Hub { get; set; }

    public ViewModel(AbbotContext db, IAuditLog auditLog, IHubRepository hubRepository, IRoomRepository roomRepository, IEmojiLookup emojiLookup) : base(db, auditLog)
    {
        _hubRepository = hubRepository;
        _roomRepository = roomRepository;
        _emojiLookup = emojiLookup;
    }

    public async Task<IActionResult> OnGetAsync(string orgId, string id)
    {
        await InitializeDataAsync(orgId);
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(id, Organization);
        if (room is null)
        {
            return NotFound();
        }

        var hub = await _hubRepository.GetHubAsync(room);
        if (hub is null)
        {
            return NotFound();
        }

        Hub = hub;
        return Page();
    }

    protected override Task InitializeDataAsync(Organization organization)
    {
        return Task.CompletedTask;
    }
}

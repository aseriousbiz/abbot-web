using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore;

namespace Serious.Abbot.Pages.Staff.Organizations.Hubs;

public class IndexModel : OrganizationDetailPage
{
    readonly IHubRepository _hubRepository;
    readonly IRoomRepository _roomRepository;

    public required IReadOnlyList<Hub> Hubs { get; set; }

    public IndexModel(AbbotContext db, IAuditLog auditLog, IHubRepository hubRepository, IRoomRepository roomRepository) : base(db, auditLog)
    {
        _hubRepository = hubRepository;
        _roomRepository = roomRepository;
    }

    public async Task OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
        Hubs = await _hubRepository.GetAllHubsAsync(Organization);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string roomId)
    {
        await InitializeDataAsync(id);
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return TurboFlash("Unknown hub");
        }

        var hub = await _hubRepository.GetHubAsync(room);
        if (hub is null)
        {
            return TurboFlash("There is no hub for this room");
        }

        await _hubRepository.DeleteHubAsync(hub, Viewer);
        StatusMessage = "Hub deleted";

        return RedirectToPage();
    }

    protected override Task InitializeDataAsync(Organization organization)
    {
        return Task.CompletedTask;
    }
}

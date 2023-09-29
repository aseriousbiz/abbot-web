using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Customers;

public class CreatePage : UserPage
{
    readonly CustomerRepository _customerRepository;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IRoomRepository _roomRepository;

    public CreatePage(CustomerRepository customerRepository, PlaybookDispatcher playbookDispatcher, IRoomRepository roomRepository)
    {
        _customerRepository = customerRepository;
        _playbookDispatcher = playbookDispatcher;
        _roomRepository = roomRepository;
    }

    [BindProperty]
    public CustomerInputModel Input { get; set; } = new();

    public IReadOnlyList<CustomerTag> CustomerSegments { get; private set; } = null!;

    public IPaginatedList<Room> Rooms { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        await InitializePageAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Viewer.CanManageConversations())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await InitializePageAsync();
            return Page();
        }

        var rooms = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(Input.PlatformRoomIds, Organization);
        var roomsToAdd = rooms.Where(r => r.Room is not null).Select(r => r.Room!);

        var created = await _customerRepository.CreateCustomerAsync(Input.Name, roomsToAdd, Viewer, Organization, null);
        var customerTagIds = Input.SegmentIds.Select(id => new Id<CustomerTag>(id));
        await _customerRepository.AssignCustomerToSegmentsAsync(created, customerTagIds, Viewer);

        var outputs = new OutputsBuilder()
            .SetCustomer(created)
            .Outputs;

        await _playbookDispatcher.DispatchAsync(
            CustomerCreatedTrigger.Id,
            outputs,
            Organization,
            PlaybookRunRelatedEntities.From(created));

        StatusMessage = "Customer created!";
        return RedirectToPage("Index");
    }

    async Task InitializePageAsync()
    {
        Rooms = await _roomRepository.GetPersistentRoomsAsync(
            Organization,
            default,
            TrackStateFilter.Tracked,
            1,
            int.MaxValue);

        CustomerSegments = await _customerRepository.GetAllCustomerSegmentsAsync(Organization, 1, int.MaxValue);
    }
}

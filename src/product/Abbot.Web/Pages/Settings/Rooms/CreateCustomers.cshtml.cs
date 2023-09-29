using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Filters;

namespace Serious.Abbot.Pages.Settings.Rooms;

public class CreateCustomersPage : UserPage
{
    readonly IRoomRepository _roomRepository;
    readonly CustomerRepository _customerRepository;

    public CreateCustomersPage(IRoomRepository roomRepository, CustomerRepository customerRepository)
    {
        _roomRepository = roomRepository;
        _customerRepository = customerRepository;
    }

    public string? CommonPrefix { get; private set; }

    [BindProperty]
    public IReadOnlyList<string?> CustomerNames { get; set; } = null!;

    [BindProperty]
    public IReadOnlyList<Id<Room>> RoomIds { get; set; } = null!;

    public IReadOnlyList<Room> Rooms { get; set; } = null!;

    public async Task OnGetAsync()
    {
        await InitializeAsync();

        CustomerNames = Rooms
            .Select(r => r.Name!) // We filter out null names in InitializeAsync.
            .Select(CustomerNameFromRoomName)
            .ToArray();
        CommonPrefix = CustomerNames.Count > 1
            ? CustomerNames.WhereNotNull().FindLongestCommonPrefix()
            : null;

        RoomIds = Rooms.Select(r => (Id<Room>)r).ToArray();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Expect.True(RoomIds.Count == CustomerNames.Count, "The Customer names and room ids must be the same length.");

        var customerToCreate = CustomerNames.Zip(RoomIds, (name, id) => (name, id));
        int customersCreated = 0;
        int roomAssignedCount = 0;
        int roomsSkippedCount = 0;
        foreach (var (customerName, roomId) in customerToCreate)
        {
            if (customerName is not { Length: > 0 })
            {
                roomsSkippedCount++;
                // Skip this customer if the name is empty.
                continue;
            }
            var room = await _roomRepository.GetRoomAsync(roomId);
            Expect.True(room != null && room.OrganizationId == Organization.Id);

            // Is there a customer with the name already?
            var customer = await _customerRepository.GetCustomerByNameAsync(customerName, Organization);

            if (room.Customer is null && customerName is { Length: > 0 })
            {
                if (customer is null)
                {
                    await _customerRepository.CreateCustomerAsync(customerName, new[] { room }, Viewer, Organization, null);
                    customersCreated++;
                }
                else
                {
                    await _customerRepository.AssignCustomerToRoomsAsync(customer, new[] { room.PlatformRoomId });
                    roomAssignedCount++;
                }
            }
        }

        int roomsAlreadyWithCustomers = RoomIds.Count - customersCreated - roomAssignedCount - roomsSkippedCount;

        var roomAlreadyWithCustomers = roomsAlreadyWithCustomers > 0
            ? $" and found {roomsAlreadyWithCustomers.ToQuantity("room")} already with customers"
            : "";
        var roomsAssigned = roomAssignedCount > 0
            ? $" and assigned {roomAssignedCount.ToQuantity("room")} to existing customers"
            : "";
        var roomsSkipped = roomsSkippedCount > 0
            ? $" and skipped {roomsSkippedCount.ToQuantity("room")} with no name"
            : "";

        // Set StatusMessage to the number of customers created and the number of rooms already with customers if greater than 0.
        StatusMessage = $"Created {customersCreated.ToQuantity("customer")}{roomAlreadyWithCustomers}{roomsAssigned}{roomsSkipped}.";

        return RedirectToPage();
    }

    async Task InitializeAsync()
    {
        // Fetch rooms
        var rooms = await _roomRepository.GetPersistentRoomsAsync(
            Organization,
            FilterList.Parse("customer:none", CultureInfo.InvariantCulture),
            TrackStateFilter.Tracked,
            1,
            int.MaxValue);

        Rooms = rooms.Where(r => r.Name is { Length: > 0 }).ToReadOnlyList();
    }

    static string CustomerNameFromRoomName(string roomName)
    {
        return roomName.Replace('-', ' ').Titleize();
    }
}

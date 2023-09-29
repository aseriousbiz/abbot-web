using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Shared.Filters;
using Serious.Abbot.Repositories;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Pages.Customers;

public class CustomerIndexPage : MetadataManagerPage
{
    readonly CustomerRepository _customerRepository;
    readonly IRoomRepository _roomRepository;

    public int? EditingCustomerId { get; private set; }

    public DomId CustomerListId { get; } = new("customer-list");

    [BindProperty]
    [Remote(action: "Validate", controller: "CustomerValidation", areaName: "InternalApi", AdditionalFields = "Id")]
    public required string Name { get; set; }

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    public int? PageNumber { get; init; }

    public bool RoomsExist { get; private set; }

    public FilterModel ActivityFilterModel { get; private set; } = null!;

    public FilterModel SegmentFilterModel { get; private set; } = null!;

    public FilterModel RoomFilterModel { get; private set; } = null!;

    public IPaginatedList<Customer> Customers { get; private set; } = null!;

    [BindProperty]
    public override MetadataFieldInput MetadataInput { get; set; } = new();

    [BindProperty]
    public override string? MetadataFieldToDelete { get; set; }

    public CustomerIndexPage(
        CustomerRepository customerRepository,
        IRoomRepository roomRepository,
        IMetadataRepository metadataRepository)
        : base(metadataRepository, MetadataFieldType.Customer)
    {
        _customerRepository = customerRepository;
        _roomRepository = roomRepository;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await InitializePageAsync();
        return Page();
    }

    async Task InitializePageAsync()
    {
        var pageNumber = PageNumber ?? 1;

        Customers = await _customerRepository.GetAllAsync(Organization, Filter, pageNumber, WebConstants.LongPageSize);

        var segments = await _customerRepository.GetAllCustomerSegmentsAsync(Organization);

        var options = new[] { new FilterOption("No segments", "none") }
            .Concat(segments.Select(s => new FilterOption(s.Name, s.Name)));

        SegmentFilterModel = FilterModel.Create(
            options,
            Filter,
            "segment",
            "Customer Segments",
            showValueInLabel: true);

        ActivityFilterModel = FilterModel.Create(
            new[]
            {
                new FilterOption("No activity", "none"),
                new ToggleOptionModel("activity", Filter, "Recent", "recent", Include: true),
                new ToggleOptionModel("activity", Filter, "Not Recent", "recent", Include: false),
            },
            Filter,
            "activity",
            "Last activity",
            showValueInLabel: true);

        var rooms = await _roomRepository.GetConversationRoomsAsync(Organization, default, 1, int.MaxValue);
        RoomsExist = rooms.Any();
        var roomOptions = new[] { new FilterOption("Unassigned", "none") }
            .Concat(rooms.Select(r => new FilterOption(r.Name ?? "(unknown)", r.PlatformRoomId)));

        RoomFilterModel = FilterModel.Create(
            roomOptions,
            Filter,
            "room",
            "Rooms",
            showValueInLabel: true);

        await InitializeMetadataAsync();
    }

    /// <summary>
    /// Handles form submission for editing segments.
    /// </summary>
    public async Task<IActionResult> OnPostEditSegmentsAsync(
        Id<Customer> customerId,
        int[] segmentIds,
        string? segmentName,
        bool createNewSegment)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var customer = await _customerRepository.GetByIdAsync(customerId, Organization);
        if (customer?.OrganizationId != Organization.Id)
        {
            return NotFound();
        }

        var existingSegmentIds = segmentIds.Where(t => t != 0);
        if (createNewSegment && segmentName is not null)
        {
            var newSegments = await _customerRepository.GetOrCreateSegmentsByNamesAsync(
                new[] { segmentName },
                Viewer,
                Organization);
            existingSegmentIds = existingSegmentIds.Concat(newSegments.Select(t => t.Id)).ToArray();
        }

        await _customerRepository.AssignCustomerToSegmentsAsync(
            customer,
            existingSegmentIds.Select(id => new Id<CustomerTag>(id)),
            Viewer);

        var customerModel = CustomerModel.FromCustomer(customer);

        return TurboStream(
            TurboFlash("Segments updated"),
            TurboUpdate(customer.GetDomId(), "_CustomerListItem", customerModel));
    }

    /// <summary>
    /// Handles form submission for editing rooms.
    /// </summary>
    public async Task<IActionResult> OnPostEditRoomsAsync(Id<Customer> customerId, string[] roomIds)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var customer = await _customerRepository.GetByIdAsync(customerId, Organization);
        if (customer?.OrganizationId != Organization.Id)
        {
            return NotFound();
        }

        await _customerRepository.AssignCustomerToRoomsAsync(customer, roomIds);

        var customerModel = CustomerModel.FromCustomer(customer);

        return TurboStream(
            TurboFlash("Rooms updated"),
            TurboUpdate(customer.GetDomId(), "_CustomerListItem", customerModel));
    }

    public async Task<IActionResult> OnPostSaveNameAsync(Id<Customer> id, string name)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var customer = await _customerRepository.GetByIdAsync(id, Organization);
        if (customer?.OrganizationId != Organization.Id)
        {
            return NotFound();
        }

        await _customerRepository.UpdateCustomerAsync(customer, Name, customer.Rooms, Viewer, Organization);

        return TurboStream(
            TurboFlash("Customer name updated"),
            TurboUpdate(customer.GetDomId("name"), name));
    }

    public async Task<IActionResult> OnPostCreateCustomerAsync()
    {
        await _customerRepository.CreateCustomerAsync(
            Name,
            Array.Empty<Room>(),
            Viewer,
            Organization,
            null);

        await InitializePageAsync();

        return TurboStream(
            TurboFlash("Customer created"),
            TurboUpdate(CustomerListId, "_CustomerList", this));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await InitializePageAsync();
        return TurboUpdate(CustomerListId, "_CustomerList", this);
    }
}

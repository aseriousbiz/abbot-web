using System.Collections.Generic;
using System.Linq;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Customers;

public class EditPage : MetadataEditorPage<Customer, int>
{
    readonly CustomerRepository _customerRepository;
    readonly IRoomRepository _roomRepository;
    readonly IOrganizationRepository _organizationRepository;
    readonly IPublishEndpoint _publishEndpoint;

    public EditPage(
        CustomerRepository customerRepository,
        IRoomRepository roomRepository,
        IOrganizationRepository organizationRepository,
        IPublishEndpoint publishEndpoint,
        IMetadataRepository metadataRepository) : base(metadataRepository)
    {
        _customerRepository = customerRepository;
        _roomRepository = roomRepository;
        _organizationRepository = organizationRepository;
        _publishEndpoint = publishEndpoint;
    }

    [BindProperty]
    public CustomerInputModel Input { get; set; } = new();

    [BindProperty]
    public override List<EntityMetadataInput> EntityMetadataInputs { get; set; } = new();

    public Organization? SeriousOrganization { get; set; }

    public HashSet<string> CustomerRoomIds { get; private set; } = null!;

    public HashSet<int> CustomerSegmentIds { get; private set; } = null!;

    public IReadOnlyList<CustomerTag> AllCustomerSegments { get; private set; } = null!;

    public IPaginatedList<Room> AllRooms { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var customer = await InitializePageAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        Input = CustomerInputModel.FromCustomer(customer);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!Viewer.CanManageConversations())
        {
            return Forbid();
        }

        var customer = await InitializePageAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var rooms = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(Input.PlatformRoomIds, Organization);
        var selectedRooms = rooms.Where(r => r.Room is not null).Select(r => r.Room!);

        await _customerRepository.UpdateCustomerAsync(
            customer,
            Input.Name,
            selectedRooms,
            Viewer,
            Organization);

        var selectedSegments = Input.SegmentIds.Select(segmentId => new Id<CustomerTag>(segmentId));
        await _customerRepository.AssignCustomerToSegmentsAsync(customer, selectedSegments, Viewer);

        StatusMessage = "Customer updated!";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostSaveMetadataAsync(int id)
    {
        return await HandlePostSaveMetadataAsync(id);
    }

    public async Task<IActionResult> OnPostUpdateInternalCustomerAsync(int id)
    {
        if (!Viewer.IsStaff())
        {
            return NotFound();
        }

        var customer = await _customerRepository.GetByIdAsync(id, Organization);
        if (customer is not null)
        {
            var orgIdMetadata = customer.Metadata.FirstOrDefault(m => m.MetadataField.Name == "OrganizationPlatformId");
            if (orgIdMetadata is { Value: not null } && await _organizationRepository.GetAsync(orgIdMetadata.Value) is
                { } org)
            {
                await _publishEndpoint.Publish(new ResyncOrganizationCustomer()
                {
                    OrganizationId = org,
                });

                return TurboFlash("Internal customer metadata update triggered");
            }
        }

        return TurboFlash("Failed to trigger internal customer metadata update");
    }

    protected override async Task<Customer?> InitializePageAsync(int entityId)
    {
        AllRooms = await _roomRepository.GetPersistentRoomsAsync(
            Organization,
            default,
            TrackStateFilter.Tracked,
            1,
            int.MaxValue);

        AllCustomerSegments = await _customerRepository.GetAllCustomerSegmentsAsync(Organization, 1, int.MaxValue);

        var customer = await _customerRepository.GetByIdAsync(entityId, Organization);

        if (customer is not null)
        {
            if (HttpContext.IsStaffMode())
            {
                var orgIdMetadata =
                    customer.Metadata.FirstOrDefault(m => m.MetadataField.Name == "OrganizationPlatformId");

                if (orgIdMetadata is { Value: not null }
                    && await _organizationRepository.GetAsync(orgIdMetadata.Value) is { } org)
                {
                    SeriousOrganization = org;
                }
            }

            CustomerSegmentIds = customer.TagAssignments.Select(t => t.TagId).ToHashSet();
            CustomerRoomIds = customer.Rooms.Select(r => r.PlatformRoomId).ToHashSet();
            await InitializeMetadataAsync(customer);
        }

        return customer;
    }

    protected override async Task<Customer?> GetEntityAsync(int entityId, Organization organization)
    {
        return await _customerRepository.GetByIdAsync(entityId, Organization);
    }

    protected override async Task UpdateEntityMetadataAsync(Customer entity,
        Dictionary<string, string?> metadataUpdates, Member actor)
    {
        await MetadataRepository.UpdateCustomerMetadataAsync(entity, metadataUpdates, Viewer);
    }
}

using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Api;

public class CustomerApiService
{
    readonly CustomerRepository _customerRepository;
    readonly IRoomRepository _roomRepository;

    public CustomerApiService(CustomerRepository customerRepository, IRoomRepository roomRepository)
    {
        _customerRepository = customerRepository;
        _roomRepository = roomRepository;
    }

    public async Task<IReadOnlyList<CustomerInfo>> GetAllAsync(Organization organization)
    {
        var customers = await _customerRepository.GetAllAsync(organization);
        return customers.Select(c => c.ToCustomerInfo()).ToList();
    }

    public async Task<CustomerInfo?> GetAsync(int id, Organization organization)
    {
        var customer = await _customerRepository.GetByIdAsync(id, organization);

        return customer?.ToCustomerInfo();
    }

    public async Task<CustomerInfo?> GetByNameAsync(string name, Organization organization)
    {
        var customer = await _customerRepository.GetCustomerByNameAsync(name, organization);

        return customer?.ToCustomerInfo();
    }

    public async Task<CustomerInfo> CreateCustomerAsync(
        CustomerRequest request,
        Member actor,
        Organization organization)
    {
        var roomResults = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(request.Rooms, organization);
        var rooms = roomResults.Select(r => r.Room).WhereNotNull().ToList();
        var response = await _customerRepository.CreateCustomerAsync(request.Name, rooms, actor, organization, null);

        var segments = GetSegmentsFromRequest(request);

        if (!segments.All(segmentName => Tag.IsValidTagName(segmentName, allowGenerated: false)))
        {
            throw new InvalidOperationException("Invalid segment name(s).");
        }

        if (segments.Any())
        {
            var result = await _customerRepository.GetOrCreateSegmentsByNamesAsync(request.Segments, actor, organization);
            await _customerRepository.AssignCustomerToSegmentsAsync(response, result.Select(t => new Id<CustomerTag>(t.Id)), actor);
        }

        return response.ToCustomerInfo();
    }

    static IReadOnlyList<string> GetSegmentsFromRequest(CustomerRequest request) => request.Segments is { Count: > 0 }
        ? request.Segments
#pragma warning disable CS0618
        : request.Tags;
#pragma warning restore CS0618

    public async Task<CustomerInfo?> UpdateCustomerAsync(
        Id<Customer> id,
        CustomerRequest request,
        Member actor,
        Organization organization)
    {
        var customer = await _customerRepository.GetByIdAsync(id, organization);
        if (customer is null)
        {
            return null;
        }

        customer.Name = request.Name;
        customer.Rooms.Clear();
        var rooms = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(request.Rooms, organization);
        customer.Rooms.AddRange(rooms.Select(r => r.Room).WhereNotNull());
        await _customerRepository.UpdateAsync(customer, actor.User);
        var tags = await _customerRepository.GetOrCreateSegmentsByNamesAsync(request.Segments, actor, organization);
        await _customerRepository.AssignCustomerToSegmentsAsync(customer, tags.Select(t => new Id<CustomerTag>(t.Id)), actor);
        return customer.ToCustomerInfo();
    }
}

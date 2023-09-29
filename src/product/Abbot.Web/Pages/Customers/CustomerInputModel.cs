using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Customers;

public class CustomerInputModel
{
    public int Id { get; init; }

    [Remote(action: "Validate", controller: "CustomerValidation", areaName: "InternalApi", AdditionalFields = "Id")]
    public string Name { get; init; } = null!;

#pragma warning disable CA1819
    public string[] PlatformRoomIds { get; init; } = Array.Empty<string>();
    public int[] SegmentIds { get; init; } = Array.Empty<int>();
#pragma warning restore CA1819

    public static CustomerInputModel FromCustomer(Customer customer)
    {
        return new()
        {
            Id = customer.Id,
            Name = customer.Name,
            PlatformRoomIds = customer.Rooms.Select(c => c.PlatformRoomId).ToArray(),
            SegmentIds = customer.TagAssignments.Select(t => t.TagId).ToArray()
        };
    }
}


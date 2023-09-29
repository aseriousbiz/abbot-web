using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.AspNetCore.DataAnnotations;

namespace Serious.Abbot.Models;

public class CustomerModel
{
    public int Id { get; init; }

    [Remote(action: "Validate", controller: "CustomerValidation", areaName: "InternalApi", AdditionalFields = "Id")]
    public string Name { get; init; } = null!;

    [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
    [RegularExpression(Skill.ValidNamePattern, ErrorMessage = Skill.NameErrorMessage)]
    [Remote(action: "ValidateSegment", controller: "CustomerValidation", areaName: "InternalApi")]
    [RequiredIf(nameof(CreateNewSegment), true)]
    public string? SegmentName { get; set; }

    public bool CreateNewSegment { get; set; }

    public required IReadOnlyList<CustomerTag> Segments { get; init; }

    public required IReadOnlyList<Room> Rooms { get; init; }

    public required Organization Organization { get; init; }

    public required DateTime? LastMessageActivityUtc { get; init; }

    public static CustomerModel FromCustomer(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Organization = customer.Organization,
        Segments = customer.TagAssignments.Select(a => a.Tag)
            .ToReadOnlyList(),
        Rooms = customer.Rooms,
        Customer = customer,
        LastMessageActivityUtc = customer.LastMessageActivityUtc,
    };

    public required Customer Customer { get; init; }

    public DomId GetDomId(string? baseName = null) => Customer.GetDomId(baseName);
}

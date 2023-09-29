using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Customers.Segments;

public class CreatePage : UserPage
{
    readonly CustomerRepository _customerRepository;

    public CreatePage(CustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    [BindProperty]
    [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
    [RegularExpression(Skill.ValidNamePattern, ErrorMessage = Skill.NameErrorMessage)]
    [Remote(action: "ValidateSegment", controller: "CustomerValidation", areaName: "InternalApi")]
    [Display(Name = "Segment Name")]
    public string SegmentName { get; set; } = null!;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _customerRepository.CreateCustomerSegmentAsync(SegmentName, Viewer, Organization);

        StatusMessage = "Customer segment created";

        return RedirectToPage("Index");
    }
}

using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Customers.Segments;

public class DeletePage : UserPage
{
    readonly CustomerRepository _customerRepository;

    public DeletePage(CustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public CustomerTag Segment { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Id<CustomerTag> id)
    {
        var segment = await InitializeState(id);

        if (segment is null || segment.Id == Viewer.Id)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Id<CustomerTag> id)
    {
        var segment = await InitializeState(id);

        if (segment is null || segment.Id == Viewer.Id)
        {
            return NotFound();
        }

        await _customerRepository.RemoveCustomerSegmentAsync(segment, Viewer);
        StatusMessage = $"{segment.Name} segment deleted.";

        return RedirectToPage("Index");
    }

    async Task<CustomerTag?> InitializeState(Id<CustomerTag> id)
    {
        var tag = await _customerRepository.GetCustomerSegmentByIdAsync(id, Organization);
        if (tag is null)
        {
            return null;
        }

        Segment = tag;
        return tag;
    }
}

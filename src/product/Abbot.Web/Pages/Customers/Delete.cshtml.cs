using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Customers;

public class DeletePage : UserPage
{
    readonly CustomerRepository _customerRepository;

    public DeletePage(CustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public Customer Customer { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var customer = await _customerRepository.GetByIdAsync(id, Organization);
        if (customer is null)
        {
            return NotFound();
        }

        Customer = customer;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var customer = await _customerRepository.GetByIdAsync(id, Organization);
        if (customer is null)
        {
            return NotFound();
        }

        var result = await _customerRepository.RemoveCustomerAsync(customer, Viewer, Organization);

        if (!result.IsSuccess)
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}{result.ErrorMessage}";
            return RedirectToPage();
        }

        StatusMessage = $"Deleted customer {customer.Name}.";
        return RedirectToPage("Index");
    }
}

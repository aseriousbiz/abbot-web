using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Customers.Segments;

public class IndexPage : UserPage
{
    readonly CustomerRepository _customerRepository;

    public IndexPage(CustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public IPaginatedList<CustomerTag> CustomerSegments { get; private set; } = null!;

    public async Task OnGetAsync(int p = 1)
    {
        await InitializePage(p);
    }

    async Task InitializePage(int pageNumber)
    {
        CustomerSegments = await _customerRepository.GetAllCustomerSegmentsAsync(
            Organization,
            pageNumber,
            WebConstants.LongPageSize);
    }
}

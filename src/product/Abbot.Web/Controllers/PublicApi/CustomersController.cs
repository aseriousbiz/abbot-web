using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;

namespace Serious.Abbot.Controllers.PublicApi;

[ApiController]
[AbbotApiHost]
[Route("api/customers")]
[Authorize(Policy = AuthorizationPolicies.PublicApi)]
public class CustomersController : UserControllerBase
{
    readonly CustomerApiService _customerApiService;

    public CustomersController(CustomerApiService customerApiService)
    {
        _customerApiService = customerApiService;
    }


    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
    {
        return Json(await _customerApiService.GetAllAsync(Organization));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsync(int id)
    {
        var customer = await _customerApiService.GetAsync(id, Organization);
        return customer is not null
            ? Json(customer)
            : NotFound();
    }

    [HttpGet("name/{name}")]
    public async Task<IActionResult> GetByNameAsync(string name)
    {
        var customer = await _customerApiService.GetByNameAsync(name, Organization);
        return customer is not null
            ? Json(customer)
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CustomerRequest request)
    {
        var customer = await _customerApiService.CreateCustomerAsync(request, CurrentMember, Organization);
        return Json(customer);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, CustomerRequest request)
    {
        Id<Customer> customerId = new(id);
        var customer = await _customerApiService.UpdateCustomerAsync(customerId, request, CurrentMember, Organization);
        return Json(customer);
    }
}

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers;

public class CustomersController : SkillRunnerApiControllerBase
{
    readonly CustomerApiService _customerApiService;
    readonly InsightsApiService _insightsApiService;
    readonly IOrganizationRepository _organizationRepository;

    public CustomersController(
        CustomerApiService customerApiService,
        InsightsApiService insightsApiService,
        IOrganizationRepository organizationRepository)
    {
        _customerApiService = customerApiService;
        _insightsApiService = insightsApiService;
        _organizationRepository = organizationRepository;
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetAllAsync()
    {
        return Json(await _customerApiService.GetAllAsync(Organization));
    }

    [HttpGet("customers/{id:int}")]
    public async Task<IActionResult> GetAsync(int id)
    {
        var customer = await _customerApiService.GetAsync(id, Organization);
        return customer is not null
            ? Json(customer)
            : NotFound();
    }

    [HttpGet("customers/name/{name}")]
    public async Task<IActionResult> GetByNameAsync(string name)
    {
        var customer = await _customerApiService.GetByNameAsync(name, Organization);
        return customer is not null
            ? Json(customer)
            : NotFound();
    }

    [HttpPost("customers")]
    public async Task<IActionResult> CreateAsync(CustomerRequest request)
    {
        var customer = await _customerApiService.CreateCustomerAsync(request, Member, Organization);
        return Json(customer);
    }

    [HttpPut("customers/{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, CustomerRequest request)
    {
        Id<Customer> customerId = new(id);
        var customer = await _customerApiService.UpdateCustomerAsync(customerId, request, Member, Organization);
        return Json(customer);
    }

    /// <summary>
    /// Retrieves all the metadata field defined for the organization.
    /// </summary>
    /// <param name="id">The id of the customer.</param>
    /// <param name="range">The date range to get the data for.</param>
    /// <param name="tz">The timezone id.</param>
    [HttpGet("customers/{id:int}/usage")]
    public async Task<IActionResult> GetCustomerUsageStatsAsync(
        int id,
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null)
    {
        var (roomSelector, actor) = await GetRoomSelectorAndActor(id);
        if (roomSelector is null)
        {
            return NotFound();
        }

        var tags = await _insightsApiService.GetTagFrequencyAsync(actor, range, roomSelector);
        if (tags is null)
        {
            return NotFound();
        }

        var datePeriodSelector = _insightsApiService.GetDatePeriodSelector(range, tz, actor);
        var startDate = datePeriodSelector.EnumerateDays().First();
        var endDate = datePeriodSelector.EnumerateDays().Last();

        var conversationSummary = await _insightsApiService.GetSummaryAsync(actor, range, roomSelector);
        var trends = await _insightsApiService.GetTrendsAsync(actor, range, roomSelector);
        var tagCounts = tags
            .Select(t => new TagFrequencyInfo(t.Tag.Name, t.Count))
            .ToReadOnlyList();

        var usageStats = new CustomerUsageStats(
            tagCounts,
            trends.Summary,
            conversationSummary,
            startDate,
            endDate);
        return Json(usageStats);
    }

    async Task<(RoomSelector?, Member)> GetRoomSelectorAndActor(int customerId)
    {
        if (!Member.Organization.IsSerious())
        {
            // Our customers can use this API to get their own stats for their own customers.
            return (new CustomerRoomSelector(new Id<Customer>(customerId)), Member);
        }

        // But when *we* call this API, we want the stats for the customer's organization.
        if (await _customerApiService.GetAsync(customerId, Organization) is not { } customer
            || !customer.Metadata.TryGetValue("OrganizationId", out var organizationIdText)
            || !int.TryParse(organizationIdText, out var organizationId)
            || await _organizationRepository.GetAsync(organizationId) is not { } customerOrganization)
        {
            return (null, Member);
        }

        var customerAbbot = await _organizationRepository.EnsureAbbotMember(customerOrganization);
        return (RoomSelector.AllRooms, customerAbbot);
    }
}

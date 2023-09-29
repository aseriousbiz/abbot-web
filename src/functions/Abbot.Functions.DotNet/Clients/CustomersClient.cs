using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Execution;

public class CustomersClient : ICustomersClient
{
    readonly ISkillApiClient _apiClient;

    /// <summary>
    /// Constructs a <see cref="RoomsClient"/>.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to call skill runner APIs.</param>
    public CustomersClient(ISkillApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<AbbotResponse<IReadOnlyList<CustomerInfo>>> GetAllAsync()
        => await _apiClient.GetApiAsync<IReadOnlyList<CustomerInfo>, List<CustomerInfo>>(CustomersApiUrl);

    public async Task<AbbotResponse<CustomerInfo>> GetAsync(int id)
        => await _apiClient.GetApiAsync<CustomerInfo>(GetCustomerUrl(id));

    public async Task<AbbotResponse<CustomerInfo>> GetByNameAsync(string name)
        => await _apiClient.GetApiAsync<CustomerInfo>(CustomersApiUrl.Append("name").AppendEscaped(name));

    public async Task<AbbotResponse<CustomerInfo>> CreateAsync(CustomerRequest customerRequest)
        => await _apiClient.SendApiAsync<CustomerRequest, CustomerInfo>(
            CustomersApiUrl,
            HttpMethod.Post,
            customerRequest);

    public async Task<AbbotResponse<CustomerInfo>> UpdateAsync(int id, CustomerRequest customerRequest)
        => await _apiClient.SendApiAsync<CustomerRequest, CustomerInfo>(
            GetCustomerUrl(id),
            HttpMethod.Put,
            customerRequest);

    public async Task<AbbotResponse<CustomerUsageStats>> GetUsageStatsAsync(int id)
        => await _apiClient.GetApiAsync<CustomerUsageStats>(GetCustomerUrl(id).Append("usage"));

    Uri CustomersApiUrl => _apiClient.BaseApiUrl.Append("/customers");

    Uri GetCustomerUrl(int customerId) => CustomersApiUrl.AppendEscaped($"{customerId}");
}

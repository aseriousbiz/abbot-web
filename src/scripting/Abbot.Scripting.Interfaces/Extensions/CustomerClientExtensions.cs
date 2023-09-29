using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Extensions for <see cref="ICustomersClient"/>.
/// </summary>
public static class CustomerClientExtensions
{
    /// <summary>
    /// Retrieves usage stats for the specified customer.
    /// </summary>
    /// <param name="customersClient">The customers client.</param>
    /// <param name="customer">The customer.</param>
    public static async Task<AbbotResponse<CustomerUsageStats>> GetUsageStatsAsync(
        this ICustomersClient customersClient,
        CustomerInfo customer)
        => await customersClient.GetUsageStatsAsync(customer.Id);
}

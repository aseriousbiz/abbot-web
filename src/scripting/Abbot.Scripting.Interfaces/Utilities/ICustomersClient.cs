using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to manage customers.
/// </summary>
public interface ICustomersClient
{
    /// <summary>
    /// Gets all the customers in your org.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the set of customers in the org.</returns>
    Task<AbbotResponse<IReadOnlyList<CustomerInfo>>> GetAllAsync();

    /// <summary>
    /// Gets the customer with the specified Id.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the customer.</returns>
    Task<AbbotResponse<CustomerInfo>> GetAsync(int id);

    /// <summary>
    /// Gets the customer with the specified name.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the customer.</returns>
    Task<AbbotResponse<CustomerInfo>> GetByNameAsync(string name);

    /// <summary>
    /// Creates a Room and returns the created Id of the room.
    /// </summary>
    /// <param name="customerRequest">Information used to create a customer.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the created customer.</returns>
    Task<AbbotResponse<CustomerInfo>> CreateAsync(CustomerRequest customerRequest);

    /// <summary>
    /// Creates a Room and returns the created Id of the room.
    /// </summary>
    /// <param name="id">The Id of the customer to update.</param>
    /// <param name="customerRequest">Information used to create a customer.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the updated customer.</returns>
    Task<AbbotResponse<CustomerInfo>> UpdateAsync(int id, CustomerRequest customerRequest);

    /// <summary>
    /// Retrieves usage stats for the specified customer.
    /// </summary>
    /// <param name="id">The customer id.</param>
    Task<AbbotResponse<CustomerUsageStats>> GetUsageStatsAsync(int id);
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to manage tasks.
/// </summary>
public interface ITasksClient
{
    /// <summary>
    /// Gets all the customers in your org.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the set of customers in the org.</returns>
    Task<AbbotResponse<IReadOnlyList<TaskItemInfo>>> GetAllAsync();

    /// <summary>
    /// Gets the customer with the specified Id.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the customer.</returns>
    Task<AbbotResponse<TaskItemInfo>> GetAsync(int id);

    /// <summary>
    /// Creates a Task and returns the created Id of the task.
    /// </summary>
    /// <param name="taskRequest">Information used to create a customer.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the created customer.</returns>
    Task<AbbotResponse<TaskItemInfo>> CreateAsync(TaskRequest taskRequest);

    /// <summary>
    /// Creates a Task and returns the created Id of the task.
    /// </summary>
    /// <param name="id">The Id of the customer to update.</param>
    /// <param name="taskRequest">Information used to create a customer.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the updated customer.</returns>
    Task<AbbotResponse<TaskItemInfo>> UpdateAsync(int id, TaskRequest taskRequest);
}

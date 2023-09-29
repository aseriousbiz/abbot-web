using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Serious.Tasks;

public static class TaskExtensions
{
    /// <summary>
    /// Runs a task with a timeout.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="timeout">The timeout timespan.</param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public static async Task<TResult> WithTimeout<TResult>(
        this Task<TResult> task,
        TimeSpan timeout)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
            return await task.ConfigureAwait(false);

        throw new TimeoutException();
    }

    /// <summary>
    /// Runs every task one at a time and returns the results. Primarily used for running a bunch of methods against
    /// DbContext which can't be run in parallel.
    /// </summary>
    /// <param name="tasks">The set of tasks.</param>
    /// <typeparam name="T">The return type for each task.</typeparam>
    /// <returns></returns>
    public static async Task<IReadOnlyList<T>> WhenAllOneAtATimeAsync<T>(this IEnumerable<Func<Task<T?>>> tasks)
        where T : notnull
    {
        var results = new List<T>();
        foreach (var task in tasks)
        {
            var result = await task();
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Runs every task one at a time and returns the results. Primarily used for running a bunch of methods against
    /// DbContext which can't be run in parallel.
    /// </summary>
    /// <param name="tasks">The set of tasks.</param>
    /// <typeparam name="T">The return type for each task.</typeparam>
    /// <returns></returns>
    public static async Task<IReadOnlyList<T>> WhenAllOneAtATimeNotNullAsync<T>(this IEnumerable<Func<Task<T>>> tasks)
        where T : notnull
    {
        var results = new List<T>();
        foreach (var task in tasks)
        {
            var result = await task();
            results.Add(result);

        }

        return results;
    }

    /// <summary>
    /// Evaluates a queryable and wraps the result in a read only list.
    /// </summary>
    /// <param name="queryable">The queryable to evaluate.</param>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>A read only list of <typeparamref name="T"/>.</returns>
    public static async Task<IReadOnlyList<T>> ToReadOnlyListAsync<T>(this IQueryable<T> queryable)
    {
        var result = await queryable.ToListAsync();
        return result.ToReadOnlyList();
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to store information specific to your bot skill.
/// </summary>
public interface IBrain
{
    /// <summary>
    /// Reads a storage item from the brain.
    /// </summary>
    /// <param name="key">key of the item to read.</param>
    /// <returns>A task with the stored value associated with the key.</returns>
    Task<dynamic?> GetAsync(string key);

    /// <summary>
    /// Gets an item from the brain and casts it to <typeparamref name="T"/>. Returns null if it does not exist.
    /// </summary>
    /// <param name="key">key of the item to read.</param>
    /// <returns>A task with the stored value associated with the key.</returns>
    Task<T?> GetAsAsync<T>(string key);

    /// <summary>
    /// Gets an item from the brain and casts it to <typeparamref name="T"/>. Returns defaultValue if it does not exist.
    /// </summary>
    /// <param name="key">key of the item to read.</param>
    /// <param name="defaultValue">The value to return if the key is missing from storage.</param>
    /// <returns>A task with the stored value associated with the key.</returns>
    Task<T> GetAsAsync<T>(string key, T defaultValue);

    /// <summary>
    /// Writes the specified value to the brain. It overwrites an existing item.
    /// </summary>
    /// <param name="key">key of the item to store.</param>
    /// <param name="value">The item to store.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    Task WriteAsync(string key, object value);

    /// <summary>
    /// Deletes the stored item associated with the key.
    /// </summary>
    /// <param name="key">key of the stored item to delete.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    Task DeleteAsync(string key);

    /// <summary>
    /// Retrieves all of the stored keys.
    /// </summary>
    /// <returns>A task with a list of all stored keys.</returns>
    Task<IReadOnlyList<string>> GetKeysAsync(string? fuzzyKeyFilter = null);

    /// <summary>
    /// Retrieves all the values
    /// </summary>
    /// <param name="fuzzyKeyFilter">An optional fuzzy filter used to filter values</param>
    /// <returns>A task with all the stored data for this skill.</returns>
    Task<IReadOnlyList<ISkillDataItem>> GetAllAsync(string? fuzzyKeyFilter = null);
}

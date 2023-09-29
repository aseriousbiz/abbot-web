using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Functions.Clients;

/// <summary>
/// Api Client used by the brain to call the Brain API endpoint in order to store and retrieve data items
/// for a skill.
/// </summary>
/// <remarks>
/// This calls the BrainController on abbot-web.
/// </remarks>
public interface IBrainApiClient
{
    /// <summary>
    /// Retrieves a single skill data item.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    Task<SkillDataResponse?> GetSkillDataAsync(string key);

    /// <summary>
    /// Retrieves a single skill data item.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="scope"></param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    Task<SkillDataResponse?> GetSkillDataAsync(string key, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Retrieves all the skill data keys.
    /// </summary>
    Task<IReadOnlyList<string>> GetSkillDataKeysAsync();

    /// <summary>
    /// Retrieves all the data items for the skill.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllDataAsync();

    /// <summary>
    /// Deletes a single data item.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    Task DeleteDataAsync(string key);

    /// <summary>
    /// Stores a key value pair (data item) in the Bot's brain.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="value">The value of the data item.</param>
    /// <returns>A <see cref="SkillDataResponse"/> with information about the created data item.</returns>
    Task<SkillDataResponse?> PostDataAsync(string key, string value);

    /// <summary>
    /// Stores a key value pair (data item) in the Bot's brain.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="value">The value of the data item.</param>
    /// <param name="scope">The scope of the skill data item</param>
    /// /// <param name="contextId">The context id corresponding to the scope</param>
    /// <returns>A <see cref="SkillDataResponse"/> with information about the created data item.</returns>
    Task<SkillDataResponse?> PostDataAsync(string key, string value, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Deletes a single data item.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="scope">The scope of the skill data item</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    Task DeleteDataAsync(string key, SkillDataScope scope, string? contextId);
}

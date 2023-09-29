using System.Threading.Tasks;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions;

/// <summary>
/// IBrain with additional methods and properties we want to use in our internal skills
/// but not expose to public skills (yet).
/// </summary>
public interface IExtendedBrain : IBrain
{
    /// <summary>
    /// Reads a data item from the brain, with the specified scope.
    /// </summary>
    /// <param name="key">Key of the data item to read.</param>
    /// <param name="scope">What the <see cref="SkillDataScope"/> of the data is.</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    Task<object?> GetAsync(string key, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Reads a data item from the brain, with the specified scoped.
    /// </summary>
    /// <param name="key">Key of the data item to read.</param>
    /// <param name="scope">What the <see cref="SkillDataScope"/> of the data is.</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    Task<T?> GetAsAsync<T>(string key, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Writes a data item to the brain. It overwrites an existing item with matching key, scope and context id
    /// </summary>
    /// <param name="key">Key of the data item to write.</param>
    /// <param name="value">Value of the data item to write.</param>
    /// <param name="scope">What the <see cref="SkillDataScope"/> of the data is.</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    Task WriteAsync(string key, object value, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Deletes a data item from the brain with matching key, scope and context id
    /// </summary>
    /// <param name="key">Key of the data item to write.</param>
    /// <param name="scope">What the <see cref="SkillDataScope"/> of the data is.</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    Task DeleteAsync(string key, SkillDataScope scope, string? contextId);
}

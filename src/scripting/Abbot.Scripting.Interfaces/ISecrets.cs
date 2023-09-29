using System.Threading.Tasks;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to retrieve secrets stored for the skill.
/// </summary>
public interface ISecrets
{
    /// <summary>
    /// Retrieves a stored secret for the current skill using the <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The key.</param>
    /// <returns>A task with the value of the secret.</returns>
    Task<string> GetAsync(string name);
}

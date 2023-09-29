using System.Threading.Tasks;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Clients;

/// <summary>
/// Not to be confused with SignalR, use this to raise signals by calling the
/// signal api endpoint /api/skills/{id}/signal
/// </summary>
public interface ISignaler
{
    /// <summary>
    /// Raises a signal from the skill with the specified name and arguments.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="arguments">The arguments to pass to the skills that are subscribed to this signal.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<IResult> SignalAsync(string name, string arguments);
}

using System.Threading;

namespace Serious;

/// <summary>
/// Provides information about the current environment and platform.
/// </summary>
public interface IEnvironment
{
    /// <summary>
    /// Retrieve the value of an environment variable.
    /// </summary>
    /// <param name="key">The environment variable name.</param>
    /// <returns>The value of the variable or null if it doesn't exist.</returns>
    string? GetEnvironmentVariable(string key);

    /// <summary>
    /// Cancellation token that is signaled when the environment goes down.
    /// </summary>
    CancellationToken CancellationToken { get; }
}

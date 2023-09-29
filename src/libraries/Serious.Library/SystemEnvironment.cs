using System;
using System.Threading;
using Microsoft.Extensions.Hosting;

[assembly: CLSCompliant(false)]
namespace Serious;

/// <summary>
/// Provides information about the current environment and platform.
/// This is a slim wrapper of <see cref="Environment"/>.
/// </summary>
public class SystemEnvironment : IEnvironment
{
    public SystemEnvironment(IHostApplicationLifetime hostLifetime)
    {
        CancellationToken = hostLifetime.ApplicationStopping;
    }

    /// <summary>
    /// Retrieve the value of an environment variable.
    /// </summary>
    /// <param name="key">The environment variable name.</param>
    /// <returns>The value of the variable or null if it doesn't exist.</returns>
    public string? GetEnvironmentVariable(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    public CancellationToken CancellationToken { get; }
}

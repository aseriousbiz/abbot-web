using System;
using System.Threading.Tasks;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Seeds data used by the site. Runs at startup and can be used to
/// add or modify data.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Creates seed data during program startup.
    /// </summary>
    Task SeedDataAsync();

    /// <summary>
    /// Whether or not the data seeder is enabled.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Whether or not the server should be blocked from starting up until this seeder is run.
    /// </summary>
    bool BlockServerStartup => false;
}

/// <summary>
/// A <see cref="IDataSeeder"/> that can only be run once unless the version is incremented.
/// </summary>
public interface IRunOnceDataSeeder : IDataSeeder
{
    /// <summary>
    /// The version of the seeder.
    /// </summary>
    int Version { get; }
}

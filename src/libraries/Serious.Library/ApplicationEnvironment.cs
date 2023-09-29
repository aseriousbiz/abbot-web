using System;
using Microsoft.Extensions.Hosting;

namespace Serious;

public static class ApplicationEnvironment
{
    static IHostEnvironment? _instance;

    public const string Production = "production";
    public const string Staging = "staging";
    public const string Canary = "canary";
    public const string Development = "development";

    public static IHostEnvironment Instance =>
        _instance ?? throw new InvalidOperationException($"Cannot access {nameof(Instance)} before application is initialized");

    public static void Configure(IHostEnvironment environment)
    {
        _instance = environment;
    }

    /// <summary>
    /// Gets the application environment name, standardized to `lowercase`.
    /// </summary>
    // These are always simple ASCII strings and upper case is TOO SHOUTY.
    public static string Name => Instance.EnvironmentName.ToLowerInvariant();

    public static bool IsDevelopment() => Instance.IsDevelopment();
    public static bool IsProduction() => Instance.IsProduction();
    public static bool IsEnvironment(string name) => Instance.IsEnvironment(name);
}

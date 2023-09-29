using System;

namespace Serious.Abbot.Helpers;

public static class EnvironmentHostsExtensions
{
    public static string[] ParseAllowedHosts(this string? value, string[]? defaultValues = null) =>
        string.IsNullOrEmpty(value)
            ? defaultValues ?? Array.Empty<string>() // Allow any host in local development.
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

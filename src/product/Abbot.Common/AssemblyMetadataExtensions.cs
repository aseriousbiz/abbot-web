using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Serious.Abbot;

public static class AssemblyMetadataExtensions
{
    public static AssemblyBuildMetadata GetBuildMetadata(this Assembly assembly)
    {
        var metadataAttributes = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value);

        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";
        return new AssemblyBuildMetadata(
            metadataAttributes,
            informationalVersion,
            assembly.GetName().Version ?? new Version(0, 0, 0),
            assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Unknown",
            assembly.GetName().Name ?? "Unknown");
    }
}

public class AssemblyBuildMetadata
{
    readonly IReadOnlyDictionary<string, string?> _metadataAttributes;

    public string InformationalVersion { get; }
    public Version Version { get; }
    public string Configuration { get; }
    public string Name { get; }
    public string? Branch => GetOrDefault("BuildBranch");
    public string? CommitId => GetOrDefault("BuildSha");
    public string? HeadReference => GetOrDefault("BuildHeadRef");
    public string? PullRequestNumber => GetOrDefault("BuildPR");
    public DateTime? BuildDate => GetOrDefault("BuildDate") is { Length: > 0 } dateStr
        ? DateTime.ParseExact(dateStr, "O", CultureInfo.InvariantCulture)
        : null;

    public AssemblyBuildMetadata(IReadOnlyDictionary<string, string?> metadataAttributes, string informationalVersion, Version version, string configuration, string name)
    {
        _metadataAttributes = metadataAttributes;
        InformationalVersion = informationalVersion;
        Version = version;
        Configuration = configuration;
        Name = name;
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    string? GetOrDefault(string key, string? defaultValue = null)
    {
        return _metadataAttributes.TryGetValue(key, out var val) ? val : defaultValue;
    }
}

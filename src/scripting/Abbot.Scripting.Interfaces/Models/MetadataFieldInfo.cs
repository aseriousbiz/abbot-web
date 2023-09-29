namespace Serious.Abbot.Scripting;

/// <summary>
/// A metadata field defined for an organization.
/// </summary>
/// <param name="Name">The metadata field name.</param>
/// <param name="DefaultValue">The default value for the metadata field.</param>
public record MetadataFieldInfo(string Name, string? DefaultValue);


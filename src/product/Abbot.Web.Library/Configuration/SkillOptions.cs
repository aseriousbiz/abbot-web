namespace Serious.Abbot.Configuration;

/// <summary>
/// Skill Runner App settings
/// </summary>
public class SkillOptions
{
    public const string Skill = nameof(Skill);

    /// <summary>
    /// API Key for calling the Skill Api.
    /// </summary>
    public string? DataApiKey { get; set; }

    /// <summary>
    /// The URL to the .NET skill runner configured in App Settings.
    /// </summary>
    public string? DotNetEndpoint { get; set; }
    /// <summary>
    /// The function code to the .NET skill runner configured in App Settings.
    /// </summary>
    public string? DotNetEndpointCode { get; set; }

    /// <summary>
    /// The URL to the JavaScript skill runner configured in App Settings.
    /// </summary>
    public string? JavaScriptEndpoint { get; set; }

    /// <summary>
    /// The function code to the JavaScript skill runner configured in App Settings.
    /// </summary>
    public string? JavaScriptEndpointCode { get; set; }

    /// <summary>
    /// The URL to the Python skill runner configured in App Settings.
    /// </summary>
    public string? PythonEndpoint { get; set; }

    /// <summary>
    /// The function code to the Python skill runner configured in App Settings.
    /// </summary>
    public string? PythonEndpointCode { get; set; }

    /// <summary>
    /// The URL to the Ink skill runner configured in App Settings.
    /// </summary>
    public string? InkEndpoint { get; set; }

    /// <summary>
    /// The function code to the Ink skill runner configured in App Settings.
    /// </summary>
    public string? InkEndpointCode { get; set; }

    /// <summary>
    /// Settings for the key vault used to store skill secrets.
    /// </summary>
    public SkillSecretVaultOptions? SecretVault { get; set; }
}

public class SkillSecretVaultOptions
{
    public string? Name { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? TenantId { get; init; }
}

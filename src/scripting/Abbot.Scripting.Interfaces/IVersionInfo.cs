using System;

[assembly: CLSCompliant(false)]
namespace Serious.Abbot.Scripting;

/// <summary>
/// Provides info about Abbot.
/// </summary>
public interface IVersionInfo
{
    /// <summary>
    /// The current version of the Abbot Product.
    /// </summary>
    string ProductVersion { get; }

    /// <summary>
    /// The version of the API that a skill is compiled against.
    /// </summary>
    Version ApiVersion { get; }
}

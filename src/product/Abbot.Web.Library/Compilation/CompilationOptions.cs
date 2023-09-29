namespace Serious.Abbot.Compilation;

public class CompilationOptions
{
    /// <summary>
    /// The storage account to use to store compiled skill assemblies.
    /// If this is set, it is used instead of <see cref="ConnectionString"/> and assumes that managed identity is configured.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// A connection string to a storage account to use.
    /// This is ignored if <see cref="AccountName"/> is set.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of the container in which to place compiled skill assemblies.
    /// </summary>
    public string? ContainerName { get; set; }
}

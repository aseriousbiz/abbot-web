namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Options for connecting to a database.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// Overrides the port used to connect to the database.
    /// If set, this value is used over any other setting, including the connection string.
    /// </summary>
    public int? OverridePort { get; set; }
}

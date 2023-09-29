namespace Serious.Abbot.Messages;

/// <summary>
/// Determines whether the order is ascending or descending.
/// </summary>
public enum OrderDirection
{
    /// <summary>
    /// Order by ascending (smallest/earliest to largest/latest).
    /// </summary>
    Ascending,

    /// <summary>
    /// Order by ascending (largest/latest to smallest/earliest).
    /// </summary>
    Descending
}

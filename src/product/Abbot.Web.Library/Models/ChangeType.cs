namespace Serious.Abbot.Models;

public enum ChangeType
{
    /// <summary>
    /// Represents a breaking change
    /// </summary>
    Major,
    /// <summary>
    /// Represents a non-breaking new feature.
    /// </summary>
    Minor,
    /// <summary>
    /// Represents a bug fix only change.
    /// </summary>
    Patch
}

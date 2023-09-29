namespace Serious.Abbot.Playbooks.Actions;
#pragma warning disable CA1008

/// <summary>
/// A comparison type used for conditional steps.
/// </summary>
public enum ExistenceComparisonType
{
    /// <summary>
    /// The value exists.
    /// </summary>
    Exists,

    /// <summary>
    /// The value does not exist.
    /// </summary>
    NotExists,
}

/// <summary>
/// A comparison type used for conditional steps.
/// </summary>
public enum StringComparisonType
{
    /// <summary>
    /// Matches messages that start with the pattern.
    /// </summary>
    StartsWith = 1,

    /// <summary>
    /// Matches messages that end with the pattern.
    /// </summary>
    EndsWith = 2,

    /// <summary>
    /// Matches messages where the message contains the pattern.
    /// </summary>
    Contains = 3,

    /// <summary>
    /// This pattern uses regular expressions to match incoming messages.
    /// </summary>
    RegularExpression = 4,

    /// <summary>
    /// This pattern matches messages that are an exact match.
    /// </summary>
    ExactMatch = 5,
}

public enum NumberComparisonType
{
    GreaterThan = 1,
    LessThan = 2,
    GreaterThanOrEqualTo = 3,
    LessThanOrEqualTo = 4,
    Equals = 5,
    NotEquals = 6,
}

/// <summary>
/// Comparison type for comparing sets.
/// </summary>
public enum ArrayComparisonType
{
    /// <summary>
    /// If values in the output contains all of the the values in the conditional, the condition is true.
    /// </summary>
    All,

    /// <summary>
    /// If values in the output contains any of the values for the conditional, the condition is true.
    /// </summary>
    Any,
}

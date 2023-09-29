namespace Serious.Abbot.Scripting;

/// <summary>
/// The result of an operation to retrieve a value that could fail.
/// </summary>
/// <typeparam name="TValue">The type of the operation result</typeparam>
public interface IResult<out TValue> : IResult
{
    /// <summary>
    /// The value of the operation if it succeeded.
    /// </summary>
    public TValue? Value { get; }
}

/// <summary>
/// The result of an operation that could fail. If it fails, <see cref="Ok"/> will be false
/// and <see cref="Error"/> will contain the reason for the failure.
/// </summary>
public interface IResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Ok { get; }

    /// <summary>
    /// The error message if the operation failed.
    /// </summary>
    public string? Error { get; }
}

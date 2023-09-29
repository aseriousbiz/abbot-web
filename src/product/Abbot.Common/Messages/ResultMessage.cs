using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// A serializable Api Result.
/// </summary>
public class ApiResult : IResult
{
    /// <summary>
    /// Constructs a successful <see cref="ApiResult"/>.
    /// </summary>
    public ApiResult()
    {
        Ok = true;
    }

    /// <summary>
    /// Constructs a failed <see cref="ApiResult"/> with the specified error message.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    public ApiResult(string error)
    {
        Error = error;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// The error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }
}

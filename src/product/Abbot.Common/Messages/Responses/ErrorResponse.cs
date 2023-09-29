namespace Serious.Abbot.Messages;

/// <summary>
/// In some cases, when the API returns an error code such as 403, this is used as the body of the response in
/// order to provide more context.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// The message to convey to the caller.
    /// </summary>
    public string Message { get; set; } = null!;
}

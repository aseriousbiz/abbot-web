namespace Serious.Slack;

/// <summary>
/// Base class for responses that contain additional information. Pretty much most API responses should
/// implement this.
/// </summary>
/// <typeparam name="TBody">The body of the response.</typeparam>
public abstract class InfoResponse<TBody> : ApiResponse
{
    /// <summary>
    /// The body of the response.
    /// </summary>
    public abstract TBody? Body { get; init; }
}

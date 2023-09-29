namespace Serious.Abbot.Scripting;

/// <summary>
/// Gives some control over the response to an HTTP trigger request.
/// Useful for webhooks.
/// </summary>
public interface IHttpTriggerResponse
{
    /// <summary>
    /// The raw content to return as the body of the response. Cannot
    /// be set if <see cref="Content"/> is set.
    /// </summary>
    string? RawContent { get; set; }

    /// <summary>
    /// The content to return as the body of the response. This will
    /// be serialized as JSON. Cannot be set if <see cref="RawContent"/>
    /// is set.
    /// </summary>
    object? Content { get; set; }

    /// <summary>
    /// The content type to use in the response. If null, Abbot will
    /// choose the best content type using content negotiation.
    /// </summary>
    string? ContentType { get; set; }

    /// <summary>
    /// Represents the collection of Request Headers as defined in RFC 2616 that should be sent in the response
    /// to the HTTP trigger request.
    /// </summary>
    IResponseHeaders Headers { get; }
}
